using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Entities;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.OrderService.Services;

public interface IOrderCreationService
{
    Task<(bool exists, Guid orderId)> TryGetExistingOrderAsync(string idempotencyKey, CancellationToken ct);
    Task<Guid> CreateOrderAsync(decimal amount, string currency, string? customerId, string idempotencyKey, CancellationToken ct);
}

public class OrderCreationService : IOrderCreationService
{
    private readonly OrderDbContext _dbContext;
    private readonly IBus _bus;
    private readonly ILogger<OrderCreationService> _logger;

    public OrderCreationService(OrderDbContext dbContext, IBus bus, ILogger<OrderCreationService> logger)
    {
        _dbContext = dbContext;
        _bus = bus;
        _logger = logger;
    }

    public async Task<(bool exists, Guid orderId)> TryGetExistingOrderAsync(string idempotencyKey, CancellationToken ct)
    {
        var existingKey = await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(x => x.Key == idempotencyKey, ct);
        if (existingKey == null)
        {
            return (false, Guid.Empty);
        }

        if (existingKey.ExpireAt <= DateTime.UtcNow)
        {
            _dbContext.IdempotencyKeys.Remove(existingKey);
            await _dbContext.SaveChangesAsync(ct);
            return (false, Guid.Empty);
        }

        var existingOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == existingKey.OrderId, ct);
        if (existingOrder != null)
        {
            return (true, existingOrder.Id);
        }

        return (false, Guid.Empty);
    }

    public async Task<Guid> CreateOrderAsync(decimal amount, string currency, string? customerId, string idempotencyKey, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var correlationId = Activity.Current?.TraceId.ToString();

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            Id = orderId,
            CustomerId = customerId ?? "anonymous",
            Status = "PendingPayment",
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };
        _dbContext.Orders.Add(order);

        var idempotencyKeyEntity = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            OrderId = orderId,
            CreatedAt = now,
            ExpireAt = now.AddHours(24)
        };
        _dbContext.IdempotencyKeys.Add(idempotencyKeyEntity);

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _bus.Publish(new OrderCreated(orderId, amount, currency, order.CustomerId, now), ct);
        _logger.LogInformation("OrderCreated published. OrderId={OrderId}", orderId);

        return orderId;
    }
}


