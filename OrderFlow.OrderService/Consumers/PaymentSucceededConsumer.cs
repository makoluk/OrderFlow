using MassTransit;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Entities;
using OrderFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace OrderFlow.OrderService.Consumers;

public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
{
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    private readonly OrderDbContext _dbContext;
    private readonly Features.Basket.IBasketStore _basketStore;

    public PaymentSucceededConsumer(ILogger<PaymentSucceededConsumer> logger, OrderDbContext dbContext, Features.Basket.IBasketStore basketStore)
    {
        _logger = logger;
        _dbContext = dbContext;
        _basketStore = basketStore;
    }

    public async Task Consume(ConsumeContext<PaymentSucceeded> ctx)
    {
        var messageId = ctx.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var messageType = typeof(PaymentSucceeded).FullName ?? nameof(PaymentSucceeded);
        var correlationId = Activity.Current?.TraceId.ToString();

        // Idempotency kontrolü: Bu mesaj daha önce işlendi mi? (Inbox pattern)
        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AnyAsync(p => p.MessageId == messageId);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Message already processed, skipping | MessageId={MessageId} OrderId={OrderId}",
                messageId, ctx.Message.OrderId);
            return;
        }

        var m = ctx.Message;
        
        // Transaction başlat
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var order = await _dbContext.Orders.FindAsync(m.OrderId);
            
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for payment success", m.OrderId);
                
                // Mesajı işlenmiş olarak işaretle (order bulunamadı ama mesaj işlendi)
                _dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = messageType,
                    ProcessedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }

            order.PaidAtUtc = m.SucceededAtUtc;
            order.Status = "Paid";
            order.UpdatedAt = DateTime.UtcNow;

            // Payment kaydı oluştur veya güncelle
            var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == m.OrderId);
            if (payment == null)
            {
                payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = m.OrderId,
                    Status = "Succeeded",
                    Amount = m.Amount,
                    Currency = m.Currency,
                    PaymentId = m.PaymentId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                };
                _dbContext.Payments.Add(payment);
            }
            else
            {
                payment.Status = "Succeeded";
                payment.UpdatedAt = DateTime.UtcNow;
            }

            // Mesajı işlenmiş olarak işaretle (Inbox pattern)
            _dbContext.ProcessedMessages.Add(new ProcessedMessage
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                MessageType = messageType,
                ProcessedAt = DateTime.UtcNow,
                CorrelationId = correlationId
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // Clear customer's basket on successful payment
            var customerId = !string.IsNullOrWhiteSpace(m.CustomerId)
                ? m.CustomerId
                : order?.CustomerId ?? "anonymous";
            _basketStore.Clear(customerId);

            _logger.LogInformation("Order {OrderId} marked Paid at {Ts}, basket cleared for CustomerId={CustomerId}", m.OrderId, m.SucceededAtUtc, customerId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing PaymentSucceeded for OrderId={OrderId}", m.OrderId);
            throw;
        }
    }
}

