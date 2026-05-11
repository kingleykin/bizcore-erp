using Report.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks;

namespace Report.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context, ILogger logger)
        {
            // Check DB connection

            if (!await context.Database.CanConnectAsync())
            {
                logger.LogWarning("Cannot connect to database.");
                return;
            }

            // Check table exists
            var tableExists = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Invoices'")
                .SingleOrDefaultAsync() > 0;
            if (!tableExists)
            {
                logger.LogWarning("Invoices table does not exist.");
                return;
            }

            // Seed Invoices for Dashboard
            if (!await context.Invoices.AnyAsync())
            {
                context.Invoices.AddRange(
                    new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a"), CustomerName = "Công ty Công nghệ ABC", Amount = 1500, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                    new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"), CustomerName = "Tập đoàn ABC", Amount = 3200, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                    new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f"), CustomerName = "Cửa hàng Bán lẻ XYZ", Amount = 450, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-1) }
                );
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded initial invoices for reporting.");
            }
        }
    }
}
