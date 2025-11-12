using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Events;
using Serilog;
using OrderFlow.Shared.Middleware;
using OrderFlow.Shared.Logging;
using Microsoft.AspNetCore.Routing;
using OpenTelemetry.Instrumentation.MassTransit;

namespace OrderFlow.Shared.Extensions;

public static class TracingDefaults
{
    public static void ConfigureW3C()
    {
        System.Diagnostics.Activity.DefaultIdFormat = System.Diagnostics.ActivityIdFormat.W3C;
        System.Diagnostics.Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(
            new OpenTelemetry.Context.Propagation.CompositeTextMapPropagator(
                new OpenTelemetry.Context.Propagation.TextMapPropagator[]
                {
                    new OpenTelemetry.Context.Propagation.TraceContextPropagator(),
                    new OpenTelemetry.Context.Propagation.BaggagePropagator()
                }
            )
        );
    }
}

public static class ServiceCollectionTelemetryExtensions
{
    public static IServiceCollection AddDefaultOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool addMassTransitInstrumentation = false,
        bool addPrometheusMetrics = true)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = ctx =>
                    {
                        var p = ctx.Request.Path;
                        return !(p.StartsWithSegments("/metrics") ||
                                 p.StartsWithSegments("/health") ||
                                 p.StartsWithSegments("/ready"));
                    };
                });
                tracing.AddHttpClientInstrumentation();
                // Ensure MassTransit ActivitySource is captured so message publish/consume spans join the trace
                tracing.AddSource("MassTransit");
                if (addMassTransitInstrumentation)
                {
                    tracing.AddMassTransitInstrumentation();
                }

                var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
                else
                {
                    tracing.AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["JAEGER_AGENT_HOST"] ?? "jaeger";
                        var portStr = configuration["JAEGER_AGENT_PORT"];
                        options.AgentPort = int.TryParse(portStr, out var p) ? p : 6831;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                if (addPrometheusMetrics)
                {
                    metrics.AddPrometheusExporter();
                }
            });

        return services;
    }
}

public static class ApplicationBuilderPipelineExtensions
{
    public static IApplicationBuilder UseDefaultRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(opts =>
        {
            opts.GetLevel = (http, _, ex) =>
            {
                var p = http.Request.Path;
                if (ex is not null) return LogEventLevel.Error;
                if (p.StartsWithSegments("/metrics") ||
                    p.StartsWithSegments("/health") ||
                    p.StartsWithSegments("/ready"))
                    return LogEventLevel.Verbose;
                return LogEventLevel.Information;
            };
            opts.EnrichDiagnosticContext = (ctx, http) =>
                ctx.Set("RequestPath", http.Request.Path);
        });
    }

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }

    public static IApplicationBuilder MapPrometheusIfNotTesting(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsEnvironment("Testing"))
        {
            if (app is IEndpointRouteBuilder endpoints)
            {
                endpoints.MapPrometheusScrapingEndpoint();
            }
        }
        return app;
    }

    public static IApplicationBuilder UseCommonWebPipeline(this IApplicationBuilder app, bool includeExceptionMiddleware = true, bool mapPrometheus = true)
    {
        app.UseCorrelationIdLogging();
        app.UseDefaultRequestLogging();
        if (includeExceptionMiddleware)
        {
            app.UseMiddleware<ExceptionMiddleware>();
        }

        if (mapPrometheus)
        {
            var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
            if (!env.IsEnvironment("Testing"))
            {
                if (app is IEndpointRouteBuilder endpoints)
                {
                    endpoints.MapPrometheusScrapingEndpoint();
                }
            }
        }

        return app;
    }
}


