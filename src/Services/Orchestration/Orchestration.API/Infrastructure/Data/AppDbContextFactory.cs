using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orchestration.API.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            
            // This is only for design time (migrations)
            optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=OrchestrationDb;Integrated Security=True;TrustServerCertificate=True");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
