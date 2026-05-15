using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks.Infrastructure;

namespace Invoice.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Invoice.API.Domain.Entities.Invoice> Invoices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
            modelBuilder.ApplyBaseEntityConfiguration();
        }
    }
}
