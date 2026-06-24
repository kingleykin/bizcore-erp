using Customer.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.API.Infrastructure.Data.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customers>
    {
        public void Configure(EntityTypeBuilder<Customers> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FirstName).HasMaxLength(128);
            builder.Property(x => x.LastName).HasMaxLength(128);
            builder.Property(x => x.Email).HasMaxLength(128);
            builder.Property(x => x.Phone).HasMaxLength(128);
            builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(CustomerStatus.CreatedUser);
        }
    }
}
