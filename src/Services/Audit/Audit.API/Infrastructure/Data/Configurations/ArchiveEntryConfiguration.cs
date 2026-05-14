using Audit.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audit.API.Infrastructure.Data.Configurations
{
    public class ArchiveEntryConfiguration : IEntityTypeConfiguration<ArchiveEntry>
    {
        public void Configure(EntityTypeBuilder<ArchiveEntry> builder)
        {
            builder.ToTable("ArchiveEntries");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Action).HasMaxLength(300).IsRequired();

            builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.TenantId).HasMaxLength(100);

            builder.Property(x => x.CorrelationId).HasMaxLength(100);
            builder.Property(x => x.TraceId).HasMaxLength(64);
            builder.Property(x => x.SpanId).HasMaxLength(32);
            builder.Property(x => x.EntityName).HasMaxLength(200);
            builder.Property(x => x.EntityId).HasMaxLength(200);
            builder.Property(x => x.PerformedBy).HasMaxLength(200);
            builder.Property(x => x.PerformedByName).HasMaxLength(200);
            builder.Property(x => x.IpAddress).HasMaxLength(50);
            builder.Property(x => x.UserAgent).HasMaxLength(500);
            builder.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Hash).HasMaxLength(64);
            builder.Property(x => x.PreviousHash).HasMaxLength(64);
            builder.Property(x => x.RowVersion).IsRowVersion();

            builder.HasIndex(x => x.PerformedAt);
            builder.HasIndex(x => x.ServiceName);
            builder.HasIndex(x => x.ArchivedAt);
        }
    }
}
