using MassTransit;
using OrderFlow.PaymentService.Data;
using OrderFlow.Shared.Http;

namespace OrderFlow.PaymentService.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (PaymentDbContext dbContext, IBusControl bus) =>
        {
            var health = new ServiceStatus("healthy", DateTime.UtcNow, new Dictionary<string, ServiceCheck>());

            var dbHealthy = false;
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                health.Checks["database"] = new ServiceCheck(canConnect ? "healthy" : "unhealthy");
                dbHealthy = canConnect;
            }
            catch (Exception ex)
            {
                health.Checks["database"] = new ServiceCheck("unhealthy", Error: ex.Message);
                dbHealthy = false;
            }

            var busHealthy = false;
            try
            {
                var busAddress = bus.Address?.AbsoluteUri ?? "unknown";
                var isHealthy = bus != null && busAddress != "unknown";
                health.Checks["mass transit"] = new ServiceCheck(isHealthy ? "healthy" : "unhealthy", Address: busAddress);
                busHealthy = isHealthy;
            }
            catch (Exception ex)
            {
                health.Checks["mass transit"] = new ServiceCheck("unhealthy", Error: ex.Message);
                busHealthy = false;
            }

            var allHealthy = dbHealthy && busHealthy;
            if (allHealthy)
                return Results.Ok(BaseResponse<ServiceStatus>.Ok(health));
            return Results.Json(BaseResponse<string>.Fail("Service unhealthy"), statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Health")
        .WithTags("Health")
        .Produces<BaseResponse<ServiceStatus>>(StatusCodes.Status200OK)
        .Produces<BaseResponse<string>>(StatusCodes.Status503ServiceUnavailable)
        .WithOpenApi();

        app.MapGet("/ready", async (PaymentDbContext dbContext, IBusControl bus) =>
        {
            var ready = new ServiceStatus("ready", DateTime.UtcNow, new Dictionary<string, ServiceCheck>());

            var dbReady = false;
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                ready.Checks["database"] = new ServiceCheck(canConnect ? "ready" : "not ready");
                dbReady = canConnect;
            }
            catch (Exception ex)
            {
                ready.Checks["database"] = new ServiceCheck("not ready", Error: ex.Message);
                dbReady = false;
            }

            var busReady = false;
            try
            {
                var busAddress = bus.Address?.AbsoluteUri ?? "unknown";
                var isReady = bus != null && busAddress != "unknown";
                ready.Checks["mass transit"] = new ServiceCheck(isReady ? "ready" : "not ready", Address: busAddress);
                busReady = isReady;
            }
            catch (Exception ex)
            {
                ready.Checks["mass transit"] = new ServiceCheck("not ready", Error: ex.Message);
                busReady = false;
            }

            var allReady = dbReady && busReady;
            if (allReady)
                return Results.Ok(BaseResponse<ServiceStatus>.Ok(ready));
            return Results.Json(BaseResponse<string>.Fail("Service not ready"), statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Ready")
        .WithTags("Health")
        .Produces<BaseResponse<ServiceStatus>>(StatusCodes.Status200OK)
        .Produces<BaseResponse<string>>(StatusCodes.Status503ServiceUnavailable)
        .WithOpenApi();

        return app;
    }
}

public record ServiceCheck(string Status, string? Error = null, string? Address = null);
public record ServiceStatus(string Status, DateTime Timestamp, Dictionary<string, ServiceCheck> Checks);


