using Invoice.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context, ILogger logger)
        {
            logger.LogInformation("Checking database connection for seeding...");
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

            if (!await context.Invoices.AnyAsync())
            {
                logger.LogInformation("No invoices found. Starting seeding process...");
                var invoice1 = Invoice.API.Domain.Entities.Invoice.Create("Công ty Công nghệ ABC", 1500);
                invoice1.Id = Guid.Parse("f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a");
                
                var invoice2 = Invoice.API.Domain.Entities.Invoice.Create("Tập đoàn Kingley", 3200);
                invoice2.Id = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");
                
                var invoice3 = Invoice.API.Domain.Entities.Invoice.Create("Cửa hàng Bán lẻ XYZ", 450);
                invoice3.Id = Guid.Parse("9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f");

                context.Invoices.AddRange(invoice1, invoice2, invoice3);
                await context.SaveChangesAsync();
                
                logger.LogInformation("Successfully seeded initial invoices.");
            }
        }
    }
}
