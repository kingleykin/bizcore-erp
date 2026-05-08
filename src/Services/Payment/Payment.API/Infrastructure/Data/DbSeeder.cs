using Payment.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Payment.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context, ILogger logger)
        {
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
