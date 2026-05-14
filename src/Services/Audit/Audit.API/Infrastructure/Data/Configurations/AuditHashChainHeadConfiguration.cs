using Audit.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audit.API.Infrastructure.Data.Configurations
{
    public class AuditHashChainHeadConfiguration : IEntityTypeConfiguration<AuditHashChainHead>
    {
        public void Configure(EntityTypeBuilder<AuditHashChainHead> builder)
        {
            builder.ToTable("AuditHashChainHeads");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.HasIndex(x => new { x.PartitionKey, x.Sequence }).IsUnique();
        }
    }
}
