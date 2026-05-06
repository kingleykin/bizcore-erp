using Report.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace Report.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Invoice> Invoices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>().ToTable("Invoices");
            modelBuilder.Entity<Invoice>().Property(i => i.Amount).HasPrecision(18, 2);

            // MassTransit Outbox Mappings
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }
}
