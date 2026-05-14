using Orchestration.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchestration.API.Infrastructure.Data.Configurations
{
    public class FlowStepConfiguration : IEntityTypeConfiguration<FlowStep>
    {
        public void Configure(EntityTypeBuilder<FlowStep> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StepType).HasMaxLength(128);
            builder.Property(x => x.PayloadJson).HasMaxLength(8192);
        }
    }
}
