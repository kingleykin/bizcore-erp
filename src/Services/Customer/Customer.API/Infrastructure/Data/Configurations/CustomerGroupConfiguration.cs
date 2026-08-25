using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.API.Infrastructure.Data.Configurations
{
    public class CustomerGroupConfiguration : IEntityTypeConfiguration<Domain.Entities.CustomerGroup>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.CustomerGroup> builder)
        {
            builder.HasKey(g => g.Id);
            builder.HasIndex(g => g.Code).IsUnique();

            builder.Property(g => g.Code)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(g => g.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(g => g.Description)
                .HasMaxLength(500);
        }
    }
}
