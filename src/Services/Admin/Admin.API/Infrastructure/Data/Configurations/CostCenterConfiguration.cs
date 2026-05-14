using Admin.API.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
    {
        public void Configure(EntityTypeBuilder<CostCenter> builder)
        {
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.Code).IsUnique();
            
            builder.Property(c => c.Code)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(c => c.Name)
                .HasMaxLength(255)
                .IsRequired();
                
            builder.HasOne(c => c.LegalEntity)
                .WithMany(le => le.CostCenters)
                .HasForeignKey(c => c.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
