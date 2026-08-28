using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bizcore.UnitTests;

internal static class TestDbContextFactory
{
    private static DbContextOptions<TContext> CreateSqliteOptions<TContext>(SqliteConnection connection) where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .AddInterceptors(new Bizcore.BuildingBlocks.Interceptors.EntityVersionInterceptor())
            .Options;
    }

    public static Invoice.API.Infrastructure.Data.AppDbContext CreateInvoiceDbContext(SqliteConnection connection)
    {
        var context = new Invoice.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Invoice.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Payment.API.Infrastructure.Data.AppDbContext CreatePaymentDbContext(SqliteConnection connection)
    {
        var context = new Payment.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Payment.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Report.API.Infrastructure.Data.AppDbContext CreateReportDbContext(SqliteConnection connection)
    {
        var context = new Report.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Report.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Orchestration.API.Infrastructure.Data.AppDbContext CreateOrchestrationDbContext(SqliteConnection connection)
    {
        var context = new Orchestration.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Orchestration.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Order.API.Infrastructure.Data.AppDbContext CreateOrderDbContext(SqliteConnection connection)
    {
        var context = new Order.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Order.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Product.API.Infrastructure.Data.AppDbContext CreateProductDbContext(SqliteConnection connection)
    {
        var context = new Product.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Product.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static Inventory.API.Infrastructure.Data.AppDbContext CreateInventoryDbContext(SqliteConnection connection)
    {
        var context = new Inventory.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Inventory.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static DbContextOptions<Inventory.API.Infrastructure.Data.AppDbContext> CreateInventoryDbContextOptions(SqliteConnection connection)
        => CreateSqliteOptions<Inventory.API.Infrastructure.Data.AppDbContext>(connection);

    public static Customer.API.Infrastructure.Data.AppDbContext CreateCustomerDbContext(SqliteConnection connection)
    {
        var context = new Customer.API.Infrastructure.Data.AppDbContext(CreateSqliteOptions<Customer.API.Infrastructure.Data.AppDbContext>(connection));
        context.Database.EnsureCreated();
        return context;
    }

    public static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    // Legacy support or specific SQL Server integration tests should use Testcontainers
}
