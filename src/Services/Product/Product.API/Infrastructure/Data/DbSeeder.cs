using Microsoft.EntityFrameworkCore;

namespace Product.API.Infrastructure.Data
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

            var tableExists = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Products'")
                .SingleOrDefaultAsync() > 0;

            if (!tableExists)
            {
                logger.LogWarning("Table 'Products' does not exist yet. Skipping seeding.");
                return;
            }

            if (await context.Products.AnyAsync())
                return;

            logger.LogInformation("No products found. Starting seeding process...");

            var products = new[]
            {
                Domain.Entities.Product.Create("Bàn phím cơ AKKO", "Cái", 890000, "Bàn phím cơ switch đỏ, đèn LED RGB"),
                Domain.Entities.Product.Create("Chuột không dây Logitech", "Cái", 450000, "Chuột quang không dây, pin 12 tháng"),
                Domain.Entities.Product.Create("Màn hình LCD 24 inch", "Cái", 3200000, "Màn hình Full HD, tần số quét 75Hz"),
                Domain.Entities.Product.Create("Giấy in A4", "Ram", 65000, "Giấy in văn phòng 70gsm"),
                Domain.Entities.Product.Create("Mực in Canon", "Hộp", 320000, null)
            };

            var codes = new[] { "SP0001", "SP0002", "SP0003", "SP0004", "SP0005" };
            var ids = new[]
            {
                Guid.Parse("a1b2c3d4-e5f6-4a1b-8c2d-3e4f5a6b7c8d"),
                Guid.Parse("b2c3d4e5-f6a7-4b2c-9d3e-4f5a6b7c8d9e"),
                Guid.Parse("c3d4e5f6-a7b8-4c3d-0e4f-5a6b7c8d9e0f"),
                Guid.Parse("d4e5f6a7-b8c9-4d4e-1f5a-6b7c8d9e0f1a"),
                Guid.Parse("e5f6a7b8-c9d0-4e5f-2a6b-7c8d9e0f1a2b")
            };

            for (var i = 0; i < products.Length; i++)
            {
                products[i].Id = ids[i];
                products[i].Code = codes[i];
            }

            context.Products.AddRange(products);
            await context.SaveChangesAsync();

            logger.LogInformation("Successfully seeded initial products.");
        }
    }
}
