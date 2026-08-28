using Customer.API.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Customer> Customers { get; set; }
        public DbSet<Domain.Entities.CustomerGroup> CustomerGroups { get; set; }
        public DbSet<Domain.Entities.CustomerPointsTransaction> CustomerPointsTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
        }
    }
}
