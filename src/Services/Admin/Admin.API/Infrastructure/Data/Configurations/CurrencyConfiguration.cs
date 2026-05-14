using Admin.API.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
    {
        public void Configure(EntityTypeBuilder<Currency> builder)
        {
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.Code).IsUnique();
            
            builder.Property(c => c.Code)
                .HasMaxLength(3)
                .IsRequired();
                
            builder.Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(c => c.Symbol)
                .HasMaxLength(10)
                .IsRequired();
        }
    }
}
