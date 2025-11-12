namespace OrderFlow.OrderService.Entities;

public class IdempotencyKey
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpireAt { get; set; }
    
    // Navigation property
    public Order Order { get; set; } = null!;
}

