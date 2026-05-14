using Admin.API.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class BranchConfiguration : IEntityTypeConfiguration<Branch>
    {
        public void Configure(EntityTypeBuilder<Branch> builder)
        {
            builder.HasKey(b => b.Id);
            builder.HasIndex(b => b.Code).IsUnique();
            
            builder.Property(b => b.Code)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(b => b.Name)
                .HasMaxLength(255)
                .IsRequired();
                
            builder.Property(b => b.Address)
                .HasMaxLength(500);
                
            builder.HasOne(b => b.LegalEntity)
                .WithMany(le => le.Branches)
                .HasForeignKey(b => b.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
