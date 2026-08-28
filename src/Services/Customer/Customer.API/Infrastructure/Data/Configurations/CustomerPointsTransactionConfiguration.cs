using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.API.Infrastructure.Data.Configurations
{
    public class CustomerPointsTransactionConfiguration : IEntityTypeConfiguration<Domain.Entities.CustomerPointsTransaction>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.CustomerPointsTransaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => t.CustomerId);

            // Đảm bảo idempotent: 1 Order chỉ được cộng điểm đúng 1 lần dù OrderConfirmedEvent bị
            // MassTransit redeliver bao nhiêu lần.
            builder.HasIndex(t => t.OrderId).IsUnique();
        }
    }
}
