using System.Diagnostics;
using MassTransit;
using OrderFlow.StockService.Consumers;
using OrderFlow.Shared.Contracts;
using OrderFlow.Shared.Logging;
using Serilog;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderFlow.Shared.Messaging;

// W3C trace + baggage
Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;
Sdk.SetDefaultTextMapPropagator(
    new CompositeTextMapPropagator(
        [ new TraceContextPropagator(), new BaggagePropagator() ]
    )
);

var builder = Host.CreateApplicationBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.ConfigureSerilog();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("StockService"))
    .WithTracing(tracing => tracing
        .SetSampler(new AlwaysOnSampler())
        .AddSource("MassTransit")
        .AddHttpClientInstrumentation()
        .AddMassTransitInstrumentation()
        .AddJaegerExporter(options =>
        {
            options.AgentHost = "jaeger";
            options.AgentPort = 6831;
        }))
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation());

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentSucceededConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h => { h.Username("guest"); h.Password("guest"); });

        cfg.ConfigureTracingObservers();

        cfg.ReceiveEndpoint("stockservice-paymentsucceeded", e =>
        {
            e.UseInMemoryOutbox(context);
            e.PrefetchCount = 16;
            e.ConfigureConsumeTopology = false;
            e.Bind<PaymentSucceeded>();

            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
            e.DiscardFaultedMessages();

            e.ConfigureConsumer<PaymentSucceededConsumer>(context);
        });
    });
});

var host = builder.Build();
var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => tracerProvider.ForceFlush());
await host.RunAsync();
