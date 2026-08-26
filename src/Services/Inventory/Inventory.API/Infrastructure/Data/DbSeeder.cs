using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Infrastructure.Data
{
    /// <summary>
    /// Seed tồn kho ban đầu khớp với 5 sản phẩm demo được seed sẵn ở Product.API
    /// (cùng ProductId cố định) để có dữ liệu ngay khi mở màn hình Kho hàng.
    /// </summary>
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

            var tableExists = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Stocks'")
                .SingleOrDefaultAsync() > 0;

            if (!tableExists)
            {
                logger.LogWarning("Table 'Stocks' does not exist yet. Skipping seeding.");
                return;
            }

            if (await context.Stocks.AnyAsync())
                return;

            logger.LogInformation("No stock records found. Starting seeding process...");

            var seedData = new (Guid ProductId, string ProductName, int OnHand)[]
            {
                (Guid.Parse("a1b2c3d4-e5f6-4a1b-8c2d-3e4f5a6b7c8d"), "Bàn phím cơ AKKO", 50),
                (Guid.Parse("b2c3d4e5-f6a7-4b2c-9d3e-4f5a6b7c8d9e"), "Chuột không dây Logitech", 80),
                (Guid.Parse("c3d4e5f6-a7b8-4c3d-0e4f-5a6b7c8d9e0f"), "Màn hình LCD 24 inch", 20),
                (Guid.Parse("d4e5f6a7-b8c9-4d4e-1f5a-6b7c8d9e0f1a"), "Giấy in A4", 200),
                (Guid.Parse("e5f6a7b8-c9d0-4e5f-2a6b-7c8d9e0f1a2b"), "Mực in Canon", 35)
            };

            var stocks = seedData.Select(s => Domain.Entities.Stock.Create(s.ProductId, s.ProductName, s.OnHand)).ToList();
            context.Stocks.AddRange(stocks);
            await context.SaveChangesAsync();

            logger.LogInformation("Successfully seeded initial stock for {Count} products.", stocks.Count);
        }
    }
}
