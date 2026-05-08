using Audit.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AuditDbContext context, ILogger logger)
        {
            // Audit usually starts empty and builds up over time.
            await Task.CompletedTask;
        }
    }
}
