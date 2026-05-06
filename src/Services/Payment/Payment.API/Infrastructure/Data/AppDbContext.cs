using Payment.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace Payment.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Payment.API.Domain.Entities.Payment> Payments { get; set; }
        public DbSet<Payment.API.Domain.Entities.Invoice> Invoices { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>().HasKey(p => p.Id);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>()
                .Property(p => p.Status)
                .HasConversion<int>()
                .HasDefaultValue(PaymentStatus.Processing);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>()
                .Property(p => p.IdempotencyKey).HasMaxLength(256);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>()
                .Property(p => p.FailureReason).HasMaxLength(512);
            modelBuilder.Entity<Payment.API.Domain.Entities.Invoice>().ToTable("Invoices");

            // IdempotencyRecord mapping
            modelBuilder.Entity<IdempotencyRecord>(e =>
            {
                e.HasKey(x => x.Key);
                e.Property(x => x.Key).HasMaxLength(256).IsRequired();
                e.Property(x => x.RequestHash).HasMaxLength(64);
                e.HasIndex(x => x.ExpiresAt); // For cleanup queries
                e.HasIndex(x => x.PaymentId);
            });

            // MassTransit Outbox Mappings
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }
}
