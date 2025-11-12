using System.Collections.Concurrent;

namespace OrderFlow.OrderService.State;

public class OrderView
{
    public Guid OrderId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? StockReservedAtUtc { get; private set; }
    public DateTime? EmailSentAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailReason { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }

           public string Status =>
               FailedAtUtc is not null ? "Failed" :
               (CompletedAtUtc is not null ? "Completed" :
               (PaidAtUtc is null ? "PendingPayment" :
               (StockReservedAtUtc is null ? "Paid" :
               (EmailSentAtUtc is null ? "StockReserved" : "EmailSent"))));

    public void MarkPaid(DateTime ts) => PaidAtUtc ??= ts;
    public void MarkStockReserved(DateTime ts) => StockReservedAtUtc ??= ts;
    public void MarkEmailSent(DateTime ts) => EmailSentAtUtc ??= ts;
    public void MarkCompleted(DateTime ts) => CompletedAtUtc ??= ts;
    public void MarkFailed(string reason, DateTime ts) { FailReason = reason; FailedAtUtc = ts; }
}

public class OrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderView> _orders = new();

    public OrderView Create(Guid id, DateTime nowUtc) =>
        _orders[id] = new OrderView { OrderId = id, CreatedAtUtc = nowUtc };

    public bool TryGet(Guid id, out OrderView? view) => _orders.TryGetValue(id, out view);
    public OrderView GetOrAdd(Guid id, DateTime nowUtc) =>
        _orders.GetOrAdd(id, _ => new OrderView { OrderId = id, CreatedAtUtc = nowUtc });
}

