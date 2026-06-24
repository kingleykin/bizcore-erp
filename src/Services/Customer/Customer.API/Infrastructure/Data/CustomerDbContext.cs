using Microsoft.EntityFrameworkCore;
using Customer.API.Domain.Entities;
using Customer.API.Infrastructure.Data.Extensions;
using Bizcore.BuildingBlocks.Infrastructure;

namespace Customer.API.Infrastructure.Data;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<Customers> Customers => Set<Customers>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
        modelBuilder.ConfigureMassTransitOutbox();
        modelBuilder.ApplyBaseEntityConfiguration();
    }
}
