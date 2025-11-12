using MassTransit;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Entities;
using OrderFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace OrderFlow.OrderService.Consumers;

public class StockReservedConsumer : IConsumer<StockReserved>
{
    private readonly ILogger<StockReservedConsumer> _logger;
    private readonly OrderDbContext _dbContext;

    public StockReservedConsumer(ILogger<StockReservedConsumer> logger, OrderDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<StockReserved> ctx)
    {
        var messageId = ctx.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var messageType = typeof(StockReserved).FullName ?? nameof(StockReserved);
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
                _logger.LogWarning("Order {OrderId} not found for stock reservation", m.OrderId);
                
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

            order.StockReservedAtUtc = m.ReservedAtUtc;
            order.Status = "StockReserved";
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

            _logger.LogInformation("Order {OrderId} marked StockReserved at {Ts}", m.OrderId, m.ReservedAtUtc);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing StockReserved for OrderId={OrderId}", m.OrderId);
            throw;
        }
    }
}

