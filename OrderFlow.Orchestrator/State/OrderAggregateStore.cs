using System.Collections.Concurrent;

namespace OrderFlow.Orchestrator.State;

public class OrderAggregate {
    public Guid OrderId { get; init; }
    public bool Paid { get; private set; }
    public bool StockReserved { get; private set; }
    public bool EmailSent { get; private set; }
    public bool Failed { get; private set; }
    public string? FailReason { get; private set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    public void MarkPaid() => Paid = true;
    public void MarkStock() => StockReserved = true;
    public void MarkEmail() => EmailSent = true;

    public void MarkFailed(string reason) { Failed = true; FailReason = reason; }
    public bool IsCompleted => Paid && StockReserved && EmailSent && !Failed;
}

public class OrderAggregateStore {
    private readonly ConcurrentDictionary<Guid, OrderAggregate> _map = new();

    public OrderAggregate Get(Guid id) =>
        _map.GetOrAdd(id, _ => new OrderAggregate { OrderId = id });

    public void Remove(Guid id) => _map.TryRemove(id, out _);
}

