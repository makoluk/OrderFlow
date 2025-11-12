using MassTransit;

namespace OrderFlow.Orchestrator.Saga;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "";
    
    // Order bilgileri
    public Guid OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    
    // Adım takibi
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? StockReservedAtUtc { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? RefundRequestedAtUtc { get; set; }
    
    // Timeout tracking
    public Guid? PaymentTimeoutTokenId { get; set; }
}

