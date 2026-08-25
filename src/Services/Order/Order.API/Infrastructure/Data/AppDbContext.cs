using Bizcore.BuildingBlocks.Infrastructure;
using Order.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Order.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Order> Orders { get; set; }
        public DbSet<Domain.Entities.OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
            modelBuilder.ApplyBaseEntityConfiguration();
        }
    }
}
