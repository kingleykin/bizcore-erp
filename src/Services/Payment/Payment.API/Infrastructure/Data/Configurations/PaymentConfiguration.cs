using Payment.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Payment.API.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Domain.Entities.Payment>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Payment> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Amount).HasPrecision(18, 2);
            builder.Property(p => p.Status)
                .HasConversion<int>()
                .HasDefaultValue(PaymentStatus.Processing);
            builder.Property(p => p.IdempotencyKey).HasMaxLength(256);
            builder.Property(p => p.FailureReason).HasMaxLength(512);
        }
    }
}
