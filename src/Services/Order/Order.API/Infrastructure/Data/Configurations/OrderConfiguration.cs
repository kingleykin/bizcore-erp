using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Order.API.Infrastructure.Data.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Domain.Entities.Order>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Order> builder)
        {
            builder.HasKey(o => o.Id);
            builder.HasIndex(o => o.OrderNumber).IsUnique();
            builder.HasIndex(o => o.CustomerId);

            builder.Property(o => o.OrderNumber)
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(o => o.CustomerName)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(o => o.Note)
                .HasMaxLength(1000);

            builder.Property(o => o.CancelReason)
                .HasMaxLength(500);

            builder.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            builder.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
