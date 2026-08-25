using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Product.API.Infrastructure.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Domain.Entities.Product>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Product> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.Code).IsUnique();

            builder.Property(p => p.Code)
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(p => p.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(p => p.Unit)
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(p => p.Price)
                .HasPrecision(18, 2);

            builder.Property(p => p.Description)
                .HasMaxLength(1000);
        }
    }
}
