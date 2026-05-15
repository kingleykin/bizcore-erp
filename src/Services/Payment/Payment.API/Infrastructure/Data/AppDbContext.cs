using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks.Infrastructure;

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
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
            modelBuilder.ApplyBaseEntityConfiguration();
        }
    }
}
