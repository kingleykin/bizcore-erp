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

                var invoice2 = new Invoice { Id = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"), Status = 0 };
                var invoice3 = new Invoice { Id = Guid.Parse("9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f"), Status = 0 };

                context.Invoices.AddRange(invoice1, invoice2, invoice3);
                await context.SaveChangesAsync();
                 logger.LogInformation("Seeded initial invoice for payment tracking.");
            }
        }
    }
}
