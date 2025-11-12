using System.Data.Common;
using FluentAssertions;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.OrderService.Consumers;
using OrderFlow.OrderService.Data;
using OrderFlow.OrderService.Entities;
using OrderFlow.OrderService.Features.Basket;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Tests;

public class PaymentSucceededConsumerTests
{
    private static OrderDbContext CreateContext(out DbConnection connection)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlite(conn)
            .Options;
        var ctx = new OrderDbContext(options);
        ctx.Database.EnsureCreated();
        connection = conn;
        return ctx;
    }

    [Fact]
    public async Task Clears_Basket_For_Event_CustomerId()
    {
        using var ctx = CreateContext(out var conn);
        await using var _ = conn;

        var orderId = Guid.NewGuid();
        ctx.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = "fallback-customer",
            Status = "PendingPayment",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var basketMock = new Mock<IBasketStore>();
        var loggerMock = new Mock<ILogger<PaymentSucceededConsumer>>();
        var consumer = new PaymentSucceededConsumer(loggerMock.Object, ctx, basketMock.Object);

        var evt = new PaymentSucceeded(orderId, "evt-customer", "p-1", 10m, "TRY", DateTime.UtcNow);
        var consumeCtx = new Mock<ConsumeContext<PaymentSucceeded>>();
        consumeCtx.SetupGet(c => c.Message).Returns(evt);
        consumeCtx.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        await consumer.Consume(consumeCtx.Object);

        basketMock.Verify(b => b.Clear("evt-customer"), Times.Once);

        var order = await ctx.Orders.FindAsync(orderId);
        order!.Status.Should().Be("Paid");
    }
}


