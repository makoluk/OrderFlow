using System.Net.Http.Headers;
using MassTransit;
using OrderFlow.PaymentService.Consumers;
using OrderFlow.PaymentService.Data;
using OrderFlow.Shared.Logging;
using OrderFlow.Shared.Http;
using Microsoft.EntityFrameworkCore;
using Polly;
using OrderFlow.Shared.Extensions;
using OrderFlow.Shared.Messaging;
using OrderFlow.PaymentService.Endpoints;
using OrderFlow.PaymentService.Policies;

// W3C trace + baggage
TracingDefaults.ConfigureW3C();

var builder = WebApplication.CreateBuilder(args);

// ---- Logging
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();

// ---- OpenTelemetry (default, Jaeger/OTLP auto based on config)
builder.Services.AddDefaultOpenTelemetry(builder.Configuration, "PaymentService", addMassTransitInstrumentation: true, addPrometheusMetrics: true);

// ---- EF Core SQLite
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlite("Data Source=payments.db"));

// ---- HttpClient (BankMock)
builder.Services.AddHttpClient("bank", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["BankMock:BaseUrl"] ?? "http://localhost:5299"); // portunu burada ayarla
    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    c.Timeout = TimeSpan.FromSeconds(10); // HttpClient timeout (Polly timeout'tan daha uzun olmalı)
})
.AddPolicyHandler((services, request) =>
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    // Policy sırası: Timeout -> Retry -> CircuitBreaker (dıştan içe)
    return Policy.WrapAsync(
        HttpClientPolicies.GetTimeoutPolicy(),
        HttpClientPolicies.GetRetryPolicy(logger),
        HttpClientPolicies.GetCircuitBreakerPolicy(logger)
    );
});

// ---- MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<RefundRequestedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureTracingObservers();

        cfg.ReceiveEndpoint("paymentservice-ordercreated", e =>
        {
            e.UseInMemoryOutbox(context);
            e.PrefetchCount = 16;
            e.ConfigureConsumeTopology = false;
            e.Bind<OrderFlow.Shared.Contracts.OrderCreated>();

            // Retry policy: 5 exponential retries (1s-30s, 5s delta)
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));

            // Circuit breaker: opens after 50% failures in 1 minute, resets after 30s
            e.UseCircuitBreaker(cb =>
            {
                cb.ActiveThreshold = 10;
                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                cb.TripThreshold = 5; // 5 out of 10 = 50% failure rate
                cb.ResetInterval = TimeSpan.FromSeconds(30);
            });

            // DLQ: Discard faulted messages after retries exhausted (RabbitMQ will handle DLQ automatically)
            e.DiscardFaultedMessages();

            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("paymentservice-refundrequested", e =>
        {
            e.UseInMemoryOutbox(context);
            e.PrefetchCount = 16;
            e.ConfigureConsumeTopology = false;
            e.Bind<OrderFlow.Shared.Contracts.RefundRequested>();

            // Retry policy: 5 exponential retries (1s-30s, 5s delta)
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));

            // Circuit breaker: opens after 50% failures in 1 minute, resets after 30s
            e.UseCircuitBreaker(cb =>
            {
                cb.ActiveThreshold = 10;
                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                cb.TripThreshold = 5; // 5 out of 10 = 50% failure rate
                cb.ResetInterval = TimeSpan.FromSeconds(30);
            });

            // DLQ: Discard faulted messages after retries exhausted (RabbitMQ will handle DLQ automatically)
            e.DiscardFaultedMessages();

            e.ConfigureConsumer<RefundRequestedConsumer>(context);
        });
    });
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    dbContext.Database.EnsureCreated();
}

// Common pipeline, SRP-friendly explicit calls
app.UseCorrelationIdLogging();
app.UseDefaultRequestLogging();
app.UseGlobalExceptionHandling();
app.MapPrometheusIfNotTesting();

app.MapGet("/", () => Results.Ok(BaseResponse<string>.Ok("PaymentService up")))
   .WithName("PaymentServiceRoot")
   .WithTags("Info")
   .Produces<BaseResponse<string>>(StatusCodes.Status200OK)
   .WithOpenApi();

// Health endpoints via extension
app.MapHealthEndpoints();

app.Run();

public partial class Program { }
namespace OrderFlow.PaymentService { public class EntryPoint { } }