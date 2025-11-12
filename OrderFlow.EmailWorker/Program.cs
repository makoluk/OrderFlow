using System.Diagnostics;
using MassTransit;
using OrderFlow.EmailWorker.Consumers;
using OrderFlow.Shared.Contracts;
using OrderFlow.Shared.Logging;
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

builder.Logging.ClearProviders();
builder.ConfigureSerilog();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("EmailWorker"))
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentSucceededConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h => { h.Username("guest"); h.Password("guest"); });

        cfg.ConfigureTracingObservers();

        cfg.ReceiveEndpoint("emailworker-paymentsucceeded", e =>
        {
            e.UseInMemoryOutbox(context);
            // Retry policy: 5 exponential retries (1s-30s, 5s delta) - updated from 3 to 5
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));
            e.PrefetchCount = 16;
            e.ConfigureConsumeTopology = false;
            e.Bind<PaymentSucceeded>();

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
