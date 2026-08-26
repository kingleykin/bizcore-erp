using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.API.Infrastructure.Data.Configurations
{
    public class StockTransactionConfiguration : IEntityTypeConfiguration<Domain.Entities.StockTransaction>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.StockTransaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => t.ProductId);
            builder.HasIndex(t => t.CreatedAt);

            builder.Property(t => t.ProductName)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(t => t.Note)
                .HasMaxLength(500);
        }
    }
}
