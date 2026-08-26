using Bizcore.BuildingBlocks.Infrastructure;
using Inventory.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Stock> Stocks { get; set; }
        public DbSet<Domain.Entities.StockTransaction> StockTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
            modelBuilder.ApplyBaseEntityConfiguration();
        }
    }
}
