using Payment.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Payment.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context, ILogger logger)
        {
            if (!await context.Database.CanConnectAsync())
            {
                logger.LogError("Cannot connect to database. Seeding aborted.");
                return;
            }

            // Kiểm tra sự tồn tại của bảng Invoices
            var tableExists = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Invoices'")
                .SingleOrDefaultAsync() > 0;

            if (!tableExists)
            {
                logger.LogWarning("Table 'Invoices' does not exist yet. Skipping seeding.");
                return;
            }

            // Payment usually doesn't have initial data, but we can seed some invoices if needed for testing
            if (!await context.Invoices.AnyAsync())
            {
                 var invoice1 = new Invoice { Id = Guid.Parse("f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a"), Status = 0 };
                 context.Invoices.Add(invoice1);
                 await context.SaveChangesAsync();
                 logger.LogInformation("Seeded initial invoice for payment tracking.");
            }
        }
    }
}
