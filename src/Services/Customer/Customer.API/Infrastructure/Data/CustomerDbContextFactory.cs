using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Customer.API.Infrastructure.Data
{
    public class CustomerDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
    {
        public CustomerDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();

            // This is only for design time (migrations)
            optionsBuilder.UseSqlServer("Data Source=localhost,1433;Initial Catalog=CustomerDB;Integrated Security=True;TrustServerCertificate=True");

            return new CustomerDbContext(optionsBuilder.Options);
        }
    }
}
