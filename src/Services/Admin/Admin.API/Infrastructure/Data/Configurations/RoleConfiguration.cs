using Admin.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(r => r.Id);
            builder.HasIndex(r => r.Name).IsUnique();
            
            builder.Property(r => r.Name)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(r => r.Description)
                .HasMaxLength(500);
        }
    }
}
