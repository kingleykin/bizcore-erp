using Admin.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class NavigationMenuConfiguration : IEntityTypeConfiguration<NavigationMenu>
    {
        public void Configure(EntityTypeBuilder<NavigationMenu> builder)
        {
            builder.HasKey(n => n.Id);
            
            builder.Property(n => n.Name)
                .HasMaxLength(100)
                .IsRequired();
                
            builder.Property(n => n.Route)
                .HasMaxLength(300)
                .IsRequired();
                
            builder.Property(n => n.PermissionCode)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(n => n.Icon)
                .HasMaxLength(100);
                
            builder.HasIndex(n => n.PermissionCode);
            builder.HasIndex(n => n.SortOrder);
            
            builder.HasOne(n => n.Parent)
                .WithMany(n => n.Children)
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }
}
