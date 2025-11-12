using Microsoft.EntityFrameworkCore;
using OrderFlow.OrderService.Entities;

namespace OrderFlow.OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }
    public DbSet<ProcessedMessage> ProcessedMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.FailReason).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).IsConcurrencyToken();
            
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.PaymentId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).IsConcurrencyToken();
            
            entity.HasOne(e => e.Order)
                .WithOne(e => e.Payment)
                .HasForeignKey<Payment>(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CorrelationId);
        });

        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            
            entity.HasOne(e => e.Order)
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ExpireAt);
        });

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.ProcessedAt);
        });
    }
}

