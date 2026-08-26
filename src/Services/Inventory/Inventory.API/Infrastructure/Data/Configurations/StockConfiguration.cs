using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.API.Infrastructure.Data.Configurations
{
    public class StockConfiguration : IEntityTypeConfiguration<Domain.Entities.Stock>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Stock> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.ProductId).IsUnique();

            builder.Property(s => s.ProductName)
                .HasMaxLength(255)
                .IsRequired();
        }
    }
}
