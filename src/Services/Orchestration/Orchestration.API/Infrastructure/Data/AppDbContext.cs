using Microsoft.EntityFrameworkCore;
using Orchestration.API.Domain.Entities;

namespace Orchestration.API.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessFlow> ProcessFlows => Set<ProcessFlow>();
    public DbSet<FlowStep> FlowSteps => Set<FlowStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessFlow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InvoiceId).IsUnique();
            e.Property(x => x.FlowType).HasMaxLength(64);
            e.Property(x => x.CurrentState).HasMaxLength(128);
            e.HasMany(x => x.Steps)
                .WithOne(x => x.ProcessFlow)
                .HasForeignKey(x => x.ProcessFlowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlowStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StepType).HasMaxLength(128);
            e.Property(x => x.PayloadJson).HasMaxLength(8192);
        });
    }
}
