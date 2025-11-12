using System.Diagnostics;
using MassTransit;
using OrderAggregateStore = OrderFlow.Orchestrator.State.OrderAggregateStore;
using OrderFlow.Orchestrator.Saga;
using OrderFlow.Orchestrator.Data;
using OrderFlow.Shared.Logging;
using Microsoft.EntityFrameworkCore;
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
    .ConfigureResource(resource => resource.AddService("Orchestrator"))
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

// EF Core SQLite for Saga Repository
builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseSqlite("Data Source=saga.db"));

// State (legacy) no longer needed; keeping for backward compatibility only
builder.Services.AddSingleton<OrderAggregateStore>();

builder.Services.AddMassTransit(x =>
{
    // Enable delayed message scheduler on RabbitMQ for saga timeouts
    x.AddDelayedMessageScheduler();

    // Saga (EF-backed)
    x.AddSagaStateMachine<OrderSaga, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<SagaDbContext>();
            r.UseSqlite();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h => { h.Username("guest"); h.Password("guest"); });

        cfg.ConfigureTracingObservers();
        cfg.UseDelayedMessageScheduler();

        // Single saga endpoint handles all correlated events
        cfg.ReceiveEndpoint("orchestrator-order-saga", e =>
        {
            e.UseInMemoryOutbox(context);
            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
            e.DiscardFaultedMessages();
            e.ConfigureSaga<OrderState>(context);
        });
    });
});

var host = builder.Build();

// Ensure saga database is created
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
    dbContext.Database.EnsureCreated();
}

var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => tracerProvider.ForceFlush());
await host.RunAsync();
