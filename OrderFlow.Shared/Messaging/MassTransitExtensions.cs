using MassTransit;

namespace OrderFlow.Shared.Messaging;

public static class MassTransitExtensions
{
    public static void ConfigureTracingObservers(this IRabbitMqBusFactoryConfigurator cfg)
    {
        cfg.ConnectConsumeObserver(new OrderFlow.Shared.Contracts.Tracing.TraceContextConsumeObserver());
        cfg.ConnectPublishObserver(new OrderFlow.Shared.Contracts.Tracing.TraceContextPublishObserver());
    }
}


