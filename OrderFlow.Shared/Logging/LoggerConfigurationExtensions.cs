using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Context;

namespace OrderFlow.Shared.Logging;

public static class LoggerConfigurationExtensions
{
    public static void ConfigureSerilog(this IHostBuilder host)
    {
        host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        });
    }

    public static void ConfigureSerilog(this HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(cfg =>
        {
            cfg.ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] [{TraceId}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        });
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationIdLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var traceId = context.TraceIdentifier;
            var correlationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? traceId;
            
            using (LogContext.PushProperty("TraceId", traceId))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }
}

