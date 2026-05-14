using Admin.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.Code).IsUnique();
            
            builder.Property(p => p.Code)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(p => p.Resource)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(p => p.Scope)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(p => p.Description)
                .HasMaxLength(500);
        }
    }
}
