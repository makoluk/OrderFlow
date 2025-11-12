using MassTransit;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Entities;
using OrderFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace OrderFlow.OrderService.Consumers;

public class OrderFailedConsumer : IConsumer<OrderFailed>
{
    private readonly ILogger<OrderFailedConsumer> _logger;
    private readonly OrderDbContext _dbContext;
    
    public OrderFailedConsumer(ILogger<OrderFailedConsumer> l, OrderDbContext dbContext)
    {
        _logger = l;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<OrderFailed> ctx)
    {
        var messageId = ctx.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var messageType = typeof(OrderFailed).FullName ?? nameof(OrderFailed);
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

        // Transaction başlat
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var order = await _dbContext.Orders.FindAsync(ctx.Message.OrderId);
            
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for failure", ctx.Message.OrderId);
                
                // Mesajı işlenmiş olarak işaretle
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

            order.Status = "Failed";
            order.FailReason = ctx.Message.Reason;
            order.FailedAtUtc = ctx.Message.FailedAtUtc;
            order.UpdatedAt = DateTime.UtcNow;

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
            
            _logger.LogWarning("Order {OrderId} marked FAILED: {Reason}", ctx.Message.OrderId, ctx.Message.Reason);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing OrderFailed for OrderId={OrderId}", ctx.Message.OrderId);
            throw;
        }
    }
}

