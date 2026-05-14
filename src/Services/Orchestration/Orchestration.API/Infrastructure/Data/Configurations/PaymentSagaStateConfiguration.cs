using Orchestration.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchestration.API.Infrastructure.Data.Configurations
{
    public class PaymentSagaStateConfiguration : IEntityTypeConfiguration<PaymentSagaState>
    {
        public void Configure(EntityTypeBuilder<PaymentSagaState> builder)
        {
            builder.HasKey(x => x.CorrelationId);
            builder.Property(x => x.CurrentState).HasMaxLength(64);
            builder.Property(x => x.IdempotencyKey).HasMaxLength(256);
            builder.Property(x => x.FailureReason).HasMaxLength(512);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.HasIndex(x => x.PaymentId);
            builder.HasIndex(x => x.InvoiceId);
        }
    }
}
