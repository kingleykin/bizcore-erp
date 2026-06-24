using Customer.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(CustomerDbContext context, ILogger logger)
        {
            logger.LogInformation("Checking database connection for seeding...");
            if (!await context.Database.CanConnectAsync())
            {
                logger.LogError("Cannot connect to database. Seeding aborted.");
                return;
            }

            // Kiểm tra sự tồn tại của bảng Invoices
            var tableExists = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Customers'")
                .SingleOrDefaultAsync() > 0;

            if (!tableExists)
            {
                logger.LogWarning("Table 'Customers' does not exist yet. Skipping seeding.");
                return;
            }

            if (!await context.Customers.AnyAsync())
            {
                logger.LogInformation("No customers found. Starting seeding process...");
                var customer1 = Customers.Create("A", "Nguyen Van", "a@a", "1234567890", "123 Main St");
                customer1.Id = Guid.Parse("6e5ecabd-b4b3-40ef-81e9-79f95994d520");

                var customer2 = Customers.Create("B", "Tran Van", "b@b", "1234567890", "123 Main St");
                customer2.Id = Guid.Parse("6762cad4-8d89-4ef4-b7f4-d01ceef1689d");

                var customer3 = Customers.Create("C", "Le Van", "c@c", "1234567890", "123 Main St");
                customer3.Id = Guid.Parse("bd69ea4c-43d1-459b-bcc2-c9569722182a");

                context.Customers.AddRange(customer1, customer2, customer3);
                await context.SaveChangesAsync();

                logger.LogInformation("Successfully seeded initial customers.");
            }
        }
    }
}
