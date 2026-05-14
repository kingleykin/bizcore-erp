using Admin.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Username).IsUnique();
            builder.HasIndex(u => u.Email).IsUnique();
            
            builder.Property(u => u.Username)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(u => u.Email)
                .HasMaxLength(256)
                .IsRequired();
                
            builder.Property(u => u.PasswordHash)
                .IsRequired();

            // Inherited properties from BaseEntity are automatically mapped, 
            // but we can specify them if needed.
        }
    }
}
