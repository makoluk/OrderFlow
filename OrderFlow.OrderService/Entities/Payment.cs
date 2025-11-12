namespace OrderFlow.OrderService.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? PaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CorrelationId { get; set; }
    
    // Navigation property
    public Order Order { get; set; } = null!;
}

