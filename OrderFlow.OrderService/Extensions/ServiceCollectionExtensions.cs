using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.OrderService.Consumers;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Features.Basket;
using OrderFlow.Shared.Contracts;
using OrderFlow.Shared.Extensions;
using OrderFlow.Shared.Messaging;
using OrderFlow.OrderService.Services;

namespace OrderFlow.OrderService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderServiceModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Observability
        services.AddDefaultOpenTelemetry(configuration, "OrderService", addMassTransitInstrumentation: true, addPrometheusMetrics: true);

        // Swagger / Endpoints explorer
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // EF Core SQLite
        services.AddDbContext<OrderDbContext>(options => options.UseSqlite("Data Source=orders.db"));

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Features.Orders.CreateOrderHandler>());

        // Basket store (in-memory)
        services.AddSingleton<IBasketStore, InMemoryBasketStore>();

        // Application services
        services.AddScoped<IOrderCreationService, OrderCreationService>();

        // MassTransit
        services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentSucceededConsumer>();
            x.AddConsumer<StockReservedConsumer>();
            x.AddConsumer<ReceiptEmailSentConsumer>();
            x.AddConsumer<OrderCompletedConsumer>();
            x.AddConsumer<OrderFailedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("rabbitmq", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ConfigureTracingObservers();

                cfg.ReceiveEndpoint("orderservice-paymentsucceeded", e =>
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

                cfg.ReceiveEndpoint("orderservice-stockreserved", e =>
                {
                    e.UseInMemoryOutbox(context);
                    e.PrefetchCount = 16;
                    e.ConfigureConsumeTopology = false;
                    e.Bind<StockReserved>();
                    e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
                    e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
                    e.DiscardFaultedMessages();
                    e.ConfigureConsumer<StockReservedConsumer>(context);
                });

                cfg.ReceiveEndpoint("orderservice-receiptemail", e =>
                {
                    e.UseInMemoryOutbox(context);
                    e.PrefetchCount = 16;
                    e.ConfigureConsumeTopology = false;
                    e.Bind<ReceiptEmailSent>();
                    e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
                    e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
                    e.DiscardFaultedMessages();
                    e.ConfigureConsumer<ReceiptEmailSentConsumer>(context);
                });

                cfg.ReceiveEndpoint("orderservice-completed", e =>
                {
                    e.UseInMemoryOutbox(context);
                    e.PrefetchCount = 16;
                    e.ConfigureConsumeTopology = false;
                    e.Bind<OrderCompleted>();
                    e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
                    e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
                    e.DiscardFaultedMessages();
                    e.ConfigureConsumer<OrderCompletedConsumer>(context);
                });

                cfg.ReceiveEndpoint("orderservice-failed", e =>
                {
                    e.UseInMemoryOutbox(context);
                    e.PrefetchCount = 16;
                    e.ConfigureConsumeTopology = false;
                    e.Bind<OrderFailed>();
                    e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
                    e.UseCircuitBreaker(cb => { cb.ActiveThreshold = 10; cb.TrackingPeriod = TimeSpan.FromMinutes(1); cb.TripThreshold = 5; cb.ResetInterval = TimeSpan.FromSeconds(30); });
                    e.DiscardFaultedMessages();
                    e.ConfigureConsumer<OrderFailedConsumer>(context);
                });
            });
        });

        return services;
    }
}


