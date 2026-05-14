using Orchestration.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchestration.API.Infrastructure.Data.Configurations
{
    public class ProcessFlowConfiguration : IEntityTypeConfiguration<ProcessFlow>
    {
        public void Configure(EntityTypeBuilder<ProcessFlow> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.InvoiceId).IsUnique();
            builder.Property(x => x.FlowType).HasMaxLength(64);
            builder.Property(x => x.CurrentState).HasMaxLength(128);
            builder.HasMany(x => x.Steps)
                .WithOne(x => x.ProcessFlow)
                .HasForeignKey(x => x.ProcessFlowId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
