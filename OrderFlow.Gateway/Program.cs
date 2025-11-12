using OrderFlow.Shared.Logging;
using OrderFlow.Shared.Http;
using System.Threading.RateLimiting;
using OrderFlow.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// W3C trace + baggage (ensure consistent propagation across services)
TracingDefaults.ConfigureW3C();

// Logging
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();

// OpenTelemetry tracing for gateway (root spans + propagation to backends)
// Disable during tests to avoid Jaeger DNS dependency in WebApplicationFactory
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDefaultOpenTelemetry(builder.Configuration, "Gateway", addMassTransitInstrumentation: false, addPrometheusMetrics: false);
}

// CORS for local SPA (Vite dev server)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevSpa", p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyMethod()
         .AllowAnyHeader());
});

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Rate Limiting - Create rate limiters for different paths
var ordersRateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
{
    AutoReplenishment = true,
    PermitLimit = 10,
    Window = TimeSpan.FromSeconds(1)
});

var paymentsRateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
{
    AutoReplenishment = true,
    PermitLimit = 15,
    Window = TimeSpan.FromSeconds(1)
});

var defaultRateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
{
    AutoReplenishment = true,
    PermitLimit = 20,
    Window = TimeSpan.FromSeconds(1)
});

// Store rate limiters in a dictionary keyed by IP address
var ordersLimiters = new Dictionary<string, FixedWindowRateLimiter>();
var paymentsLimiters = new Dictionary<string, FixedWindowRateLimiter>();
var defaultLimiters = new Dictionary<string, FixedWindowRateLimiter>();

var app = builder.Build();

// Common pipeline, SRP-friendly explicit calls (no Prometheus in gateway)
app.UseCorrelationIdLogging();
app.UseDefaultRequestLogging();
app.UseGlobalExceptionHandling();

// Enable CORS for SPA before proxy
app.UseCors("DevSpa");

// Map reverse proxy routes with path-based rate limiting
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isTesting = context.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing");
        
        RateLimiter? limiter = null;
        Dictionary<string, FixedWindowRateLimiter>? limitersDict = null;
        
        // Select appropriate rate limiter based on path
        if (path.StartsWith("/api/orders"))
        {
            limitersDict = ordersLimiters;
            if (!limitersDict.ContainsKey(ipAddress))
            {
                limitersDict[ipAddress] = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = isTesting ? 5 : 10,
                    Window = isTesting ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1)
                });
            }
            limiter = limitersDict[ipAddress];
        }
        else if (path.StartsWith("/api/payments"))
        {
            limitersDict = paymentsLimiters;
            if (!limitersDict.ContainsKey(ipAddress))
            {
                limitersDict[ipAddress] = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = isTesting ? 5 : 15,
                    Window = isTesting ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1)
                });
            }
            limiter = limitersDict[ipAddress];
        }
        else
        {
            limitersDict = defaultLimiters;
            if (!limitersDict.ContainsKey(ipAddress))
            {
                limitersDict[ipAddress] = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = isTesting ? 5 : 20,
                    Window = isTesting ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1)
                });
            }
            limiter = limitersDict[ipAddress];
        }
        
        // Apply rate limiting
        if (limiter != null)
        {
            using var lease = await limiter.AcquireAsync(1, context.RequestAborted);
            if (!lease.IsAcquired)
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsJsonAsync(BaseResponse<string>.Fail("Rate limit exceeded"));
                return;
            }
        }
        
        await next();
    });
});

// Health check endpoint (no rate limiting)
app.MapGet("/health", () => Results.Ok(BaseResponse<object>.Ok(new { status = "healthy", service = "gateway" })))
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();

public partial class Program { }
namespace OrderFlow.Gateway { public class EntryPoint { } }