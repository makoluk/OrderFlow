namespace OrderFlow.PaymentService.Entities;

public class ProcessedMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? CorrelationId { get; set; }
}

