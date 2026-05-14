using Admin.API.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.HasKey(d => d.Id);
            builder.HasIndex(d => d.Code).IsUnique();
            
            builder.Property(d => d.Code)
                .HasMaxLength(50)
                .IsRequired();
                
            builder.Property(d => d.Name)
                .HasMaxLength(255)
                .IsRequired();
                
            builder.HasOne(d => d.Branch)
                .WithMany(b => b.Departments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.HasOne(d => d.Parent)
                .WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }
}
