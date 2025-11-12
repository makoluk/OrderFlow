using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orchestrator.Saga;

namespace OrderFlow.Orchestrator.Data;

public class SagaDbContext : MassTransit.EntityFrameworkCoreIntegration.SagaDbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield break; }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // OrderState entity configuration
        modelBuilder.Entity<OrderState>(entity =>
        {
            entity.HasKey(x => x.CorrelationId);
            entity.Property(x => x.CurrentState).IsRequired();
            entity.Property(x => x.OrderId).IsRequired();
            entity.Property(x => x.Amount);
            entity.Property(x => x.Currency);
            entity.Property(x => x.PaidAtUtc);
            entity.Property(x => x.StockReservedAtUtc);
            entity.Property(x => x.EmailSentAtUtc);
            entity.Property(x => x.FailedAtUtc);
            entity.Property(x => x.FailReason);
            entity.Property(x => x.PaymentTimeoutTokenId);
        });
    }
}

