namespace OrderFlow.Shared.Contracts;

public record OrderCreated(Guid OrderId, decimal Amount, string Currency, string CustomerId, DateTime CreatedAtUtc);

public record PaymentSucceeded(Guid OrderId, string CustomerId, string PaymentId, decimal Amount, string Currency, DateTime SucceededAtUtc);

public record StockReserved(Guid OrderId, DateTime ReservedAtUtc);

public record ReceiptEmailSent(Guid OrderId, string Email, DateTime SentAtUtc);

public record OrderCompleted(Guid OrderId, DateTime CompletedAtUtc);

public record PaymentFailed(Guid OrderId, string Reason, DateTime FailedAtUtc);

public record StockReserveFailed(Guid OrderId, string Reason, DateTime FailedAtUtc);

public record ReceiptEmailFailed(Guid OrderId, string Reason, DateTime FailedAtUtc);

public record OrderFailed(Guid OrderId, string Reason, DateTime FailedAtUtc);

public record RefundRequested(Guid OrderId, decimal Amount, string Currency, string Reason, DateTime RequestedAtUtc);

public record PaymentRefunded(Guid OrderId, string PaymentId, decimal Amount, string Currency, string RefundId, DateTime RefundedAtUtc);

public record PaymentTimeoutExpired(Guid OrderId);


