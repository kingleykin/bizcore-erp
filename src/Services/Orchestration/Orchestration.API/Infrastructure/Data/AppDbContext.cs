using Microsoft.EntityFrameworkCore;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data.Extensions;

namespace Orchestration.API.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessFlow> ProcessFlows => Set<ProcessFlow>();
    public DbSet<FlowStep> FlowSteps => Set<FlowStep>();
    public DbSet<PaymentSagaState> PaymentSagaStates => Set<PaymentSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ConfigureMassTransitOutbox();
    }
}
