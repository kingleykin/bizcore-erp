using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Order.API.Infrastructure.Data.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<Domain.Entities.OrderItem>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.OrderItem> builder)
        {
            builder.HasKey(i => i.Id);
            builder.HasIndex(i => i.OrderId);
            builder.HasIndex(i => i.ProductId);

            builder.Property(i => i.ProductName)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(i => i.UnitPrice)
                .HasPrecision(18, 2);
        }
    }
}
