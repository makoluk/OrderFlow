using System.Diagnostics;
using MassTransit;
using OrderFlow.Orchestrator.State;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Consumers;

public class PaymentFailedConsumer : IConsumer<PaymentFailed>
{
    private readonly ILogger<PaymentFailedConsumer> _logger;
    private readonly OrderAggregateStore _store;
    public PaymentFailedConsumer(ILogger<PaymentFailedConsumer> l, OrderAggregateStore s){_logger=l;_store=s;}

    public async Task Consume(ConsumeContext<PaymentFailed> ctx)
    {
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        var a = _store.Get(ctx.Message.OrderId);
        a.MarkFailed(ctx.Message.Reason);
        _logger.LogWarning("Orch: PaymentFailed -> {OrderId} ({Reason})", a.OrderId, ctx.Message.Reason);

        await ctx.Publish(new OrderFailed(a.OrderId, ctx.Message.Reason, ctx.Message.FailedAtUtc));
        _store.Remove(a.OrderId);
    }
}

