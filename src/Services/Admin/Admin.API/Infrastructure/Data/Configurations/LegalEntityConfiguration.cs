using Admin.API.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class LegalEntityConfiguration : IEntityTypeConfiguration<LegalEntity>
    {
        public void Configure(EntityTypeBuilder<LegalEntity> builder)
        {
            builder.HasKey(le => le.Id);
            builder.HasIndex(le => le.Code).IsUnique();
            
            builder.Property(le => le.Code)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(le => le.Name)
                .HasMaxLength(255)
                .IsRequired();
                
            builder.Property(le => le.TaxCode)
                .HasMaxLength(50);
                
            builder.Property(le => le.RegistrationNumber)
                .HasMaxLength(50);
                
            builder.Property(le => le.Address)
                .HasMaxLength(500);
                
            builder.Property(le => le.BaseCurrencyCode)
                .HasMaxLength(3);
        }
    }
}
