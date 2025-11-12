using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Endpoints;
using OrderFlow.Shared.Http;
using OrderFlow.Shared.Extensions;
using OrderFlow.Shared.Logging;

namespace OrderFlow.OrderService.Extensions;

public static class WebAppExtensions
{
    public static WebApplication UseOrderServicePipeline(this WebApplication app)
    {
        // DB init (skip in Testing)
        if (!app.Environment.IsEnvironment("Testing"))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Database.EnsureCreated();
        }

        // Common pipeline
        app.UseCorrelationIdLogging();
        app.UseDefaultRequestLogging();
        app.UseGlobalExceptionHandling();
        app.MapPrometheusIfNotTesting();

        // Swagger (skip in Testing)
        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Root + endpoints
        app.MapGet("/", () => Results.Ok(BaseResponse<string>.Ok("OrderService up")));
        app.MapOrderEndpoints();
        app.MapBasketEndpoints();

        return app;
    }
}


