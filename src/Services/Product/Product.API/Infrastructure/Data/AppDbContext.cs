using Bizcore.BuildingBlocks.Infrastructure;
using Product.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Product.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
            modelBuilder.ApplyBaseEntityConfiguration();
        }
    }
}
