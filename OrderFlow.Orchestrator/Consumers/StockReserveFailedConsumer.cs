using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderFlow.Orchestrator.State;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Consumers;

public class StockReserveFailedConsumer : IConsumer<StockReserveFailed>
{
    private readonly ILogger<StockReserveFailedConsumer> _logger;
    private readonly OrderAggregateStore _store;
    public StockReserveFailedConsumer(ILogger<StockReserveFailedConsumer> l, OrderAggregateStore s){_logger=l;_store=s;}

    public async Task Consume(ConsumeContext<StockReserveFailed> ctx)
    {
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        var a = _store.Get(ctx.Message.OrderId);
        a.MarkFailed(ctx.Message.Reason);
        _logger.LogWarning("Orch: StockReserveFailed -> {OrderId} ({Reason})", a.OrderId, ctx.Message.Reason);

        // Eğer ödeme daha önce başarıyla alınmışsa iade iste:
        if (a.Paid && a.Amount is not null && a.Currency is not null)
            await ctx.Publish(new RefundRequested(a.OrderId, a.Amount.Value, a.Currency!, "stock_failed", DateTime.UtcNow));

        await ctx.Publish(new OrderFailed(a.OrderId, ctx.Message.Reason, ctx.Message.FailedAtUtc));
        _store.Remove(a.OrderId);
    }
}

