using Microsoft.EntityFrameworkCore;
using OrderFlow.PaymentService.Entities;

namespace OrderFlow.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            
            // MessageId unique index for idempotency check
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.ProcessedAt);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.PaymentId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CorrelationId);
        });
    }
}

