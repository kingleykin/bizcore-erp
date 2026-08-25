using Microsoft.EntityFrameworkCore;

namespace Customer.API.Infrastructure.Data
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
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerGroups'")
                .SingleOrDefaultAsync() > 0;

            if (!tableExists)
            {
                logger.LogWarning("Table 'CustomerGroups' does not exist yet. Skipping seeding.");
                return;
            }

            if (!await context.CustomerGroups.AnyAsync())
            {
                logger.LogInformation("No customer groups found. Starting seeding process...");

                var retailGroup = Domain.Entities.CustomerGroup.Create("RETAIL", "Khách hàng bán lẻ", "Khách hàng cá nhân, mua lẻ");
                retailGroup.Id = Guid.Parse("b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e");

                var vipGroup = Domain.Entities.CustomerGroup.Create("VIP", "Khách hàng VIP", "Khách hàng có doanh số lớn, ưu đãi đặc biệt");
                vipGroup.Id = Guid.Parse("c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f");

                var wholesaleGroup = Domain.Entities.CustomerGroup.Create("WHOLESALE", "Đại lý / Bán sỉ", "Khách hàng doanh nghiệp, mua sỉ");
                wholesaleGroup.Id = Guid.Parse("d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a");

                context.CustomerGroups.AddRange(retailGroup, vipGroup, wholesaleGroup);
                await context.SaveChangesAsync();

                logger.LogInformation("Successfully seeded initial customer groups.");
            }

            if (!await context.Customers.AnyAsync())
            {
                logger.LogInformation("No customers found. Starting seeding process...");

                var retailGroupId = Guid.Parse("b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e");
                var vipGroupId = Guid.Parse("c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f");

                var customer1 = Domain.Entities.Customer.Create(
                    "KH0001", "Nguyễn Văn A", retailGroupId, email: "nguyenvana@example.com", phone: "0900000001");
                customer1.Id = Guid.Parse("e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b");

                var customer2 = Domain.Entities.Customer.Create(
                    "KH0002", "Công ty TNHH ABC", vipGroupId, taxCode: "0312345678", email: "contact@abc.com", phone: "0900000002");
                customer2.Id = Guid.Parse("f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c");

                context.Customers.AddRange(customer1, customer2);
                await context.SaveChangesAsync();

                logger.LogInformation("Successfully seeded initial customers.");
            }
        }
    }
}
