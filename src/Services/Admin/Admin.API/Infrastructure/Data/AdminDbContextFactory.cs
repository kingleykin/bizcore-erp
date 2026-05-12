using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Admin.API.Infrastructure.Data
{
    public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
    {
        public AdminDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AdminDbContext>();
            
            // This is only for design time (migrations)
            optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=AdminDb;Integrated Security=True;TrustServerCertificate=True");

            return new AdminDbContext(optionsBuilder.Options);
        }
    }
}
