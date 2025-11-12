using System.Diagnostics;
using MassTransit;
using OrderFlow.Orchestrator.State;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Consumers;

public class ReceiptEmailFailedConsumer : IConsumer<ReceiptEmailFailed>
{
    private readonly ILogger<ReceiptEmailFailedConsumer> _logger;
    private readonly OrderAggregateStore _store;
    public ReceiptEmailFailedConsumer(ILogger<ReceiptEmailFailedConsumer> l, OrderAggregateStore s){_logger=l;_store=s;}

    public async Task Consume(ConsumeContext<ReceiptEmailFailed> ctx)
    {
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        var a = _store.Get(ctx.Message.OrderId);
        a.MarkFailed(ctx.Message.Reason);
        _logger.LogWarning("Orch: EmailFailed -> {OrderId} ({Reason})", a.OrderId, ctx.Message.Reason);

        // Email genelde telafi gerektirmez, yeniden deneyebilir; burada sadece OrderFailed yayınlıyoruz:
        await ctx.Publish(new OrderFailed(a.OrderId, ctx.Message.Reason, ctx.Message.FailedAtUtc));
        _store.Remove(a.OrderId);
    }
}

