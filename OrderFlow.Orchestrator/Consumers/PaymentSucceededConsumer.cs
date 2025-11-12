using System.Diagnostics;
using MassTransit;
using OrderFlow.Orchestrator.State;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Consumers;

public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
{
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    private readonly OrderAggregateStore _store;

    public PaymentSucceededConsumer(ILogger<PaymentSucceededConsumer> logger, OrderAggregateStore store)
    { _logger = logger; _store = store; }

    public async Task Consume(ConsumeContext<PaymentSucceeded> ctx)
    {
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        var agg = _store.Get(ctx.Message.OrderId);
        // ödeme tutarını aggregate'a yaz (refund için)
        agg.Amount = ctx.Message.Amount;
        agg.Currency = ctx.Message.Currency;
        agg.MarkPaid();
        _logger.LogInformation("Orch: Paid -> {OrderId}", agg.OrderId);

        if (agg.IsCompleted)
        {
            await ctx.Publish(new OrderCompleted(agg.OrderId, DateTime.UtcNow));
            _logger.LogInformation("Orch: Completed -> {OrderId}", agg.OrderId);
            _store.Remove(agg.OrderId);
        }
    }
}

