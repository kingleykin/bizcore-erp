using Customer.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.API.Infrastructure.Data.Configurations
{
    public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
    {
        public void Configure(EntityTypeBuilder<CustomerGroup> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.NameCustomerGroup).HasMaxLength(128);
            builder.Property(x => x.Code).HasMaxLength(128);
            builder.Property(x => x.Description).HasMaxLength(8192);
        }
    }
}
