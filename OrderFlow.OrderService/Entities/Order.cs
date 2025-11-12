namespace OrderFlow.OrderService.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "anonymous";
    public string Status { get; set; } = "PendingPayment";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CorrelationId { get; set; }
    
    // Additional fields for tracking order state
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? StockReservedAtUtc { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailReason { get; set; }
    
    // Navigation property
    public Payment? Payment { get; set; }
}

