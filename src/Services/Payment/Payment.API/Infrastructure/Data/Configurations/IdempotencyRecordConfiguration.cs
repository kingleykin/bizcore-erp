using Payment.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Payment.API.Infrastructure.Data.Configurations
{
    public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.HasKey(x => x.Id); // Standardizing to Id as PK
            builder.Property(x => x.Key).HasMaxLength(256).IsRequired();
            builder.Property(x => x.RequestHash).HasMaxLength(64);
            builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("InProgress");
            builder.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => x.Key).IsUnique(); // Keep Key as unique index
            builder.HasIndex(x => x.ExpiresAt);
            builder.HasIndex(x => x.PaymentId);
        }
    }
}
