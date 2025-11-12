using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderFlow.Orchestrator.State;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Consumers;

public class StockReservedConsumer : IConsumer<StockReserved>
{
    private readonly ILogger<StockReservedConsumer> _logger;
    private readonly OrderAggregateStore _store;
    public StockReservedConsumer(ILogger<StockReservedConsumer> logger, OrderAggregateStore store)
    { _logger = logger; _store = store; }

    public async Task Consume(ConsumeContext<StockReserved> ctx)
    {
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        var agg = _store.Get(ctx.Message.OrderId);
        agg.MarkStock();
        _logger.LogInformation("Orch: Stock -> {OrderId}", agg.OrderId);

        if (agg.IsCompleted)
        {
            await ctx.Publish(new OrderCompleted(agg.OrderId, DateTime.UtcNow));
            _logger.LogInformation("Orch: Completed -> {OrderId}", agg.OrderId);
            _store.Remove(agg.OrderId);
        }
    }
}

