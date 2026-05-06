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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>().HasKey(p => p.Id);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Payment.API.Domain.Entities.Payment>()
                .Property(p => p.Status)
                .HasConversion<int>()
                .HasDefaultValue(PaymentStatus.Completed);
            modelBuilder.Entity<Payment.API.Domain.Entities.Invoice>().ToTable("Invoices");

            // MassTransit Outbox Mappings
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }
}
