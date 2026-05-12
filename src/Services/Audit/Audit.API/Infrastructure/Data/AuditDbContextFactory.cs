using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Audit.API.Infrastructure.Data
{
    public class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>();
            
            // This is only for design time (migrations)
            optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=AuditDb;Integrated Security=True;TrustServerCertificate=True");

            return new AuditDbContext(optionsBuilder.Options);
        }
    }
}
