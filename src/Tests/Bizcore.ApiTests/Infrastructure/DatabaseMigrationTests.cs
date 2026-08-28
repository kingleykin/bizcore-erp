using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Bizcore.BuildingBlocks.Infrastructure;
using Admin.API.Infrastructure.Data;
using Audit.API.Infrastructure.Data;
using Invoice.API.Infrastructure.Data;
using Payment.API.Infrastructure.Data;
using Report.API.Infrastructure.Data;
using Orchestration.API.Infrastructure.Data;
using Xunit.Abstractions;
using Microsoft.Data.SqlClient;
using FluentAssertions;
using System.Linq;
using Testcontainers.MsSql;

namespace Bizcore.ApiTests.Infrastructure;

/// <summary>
/// 🛡️ Migration Compliance Testing
/// Đảm bảo tất cả các file Migration có thể chạy thành công trên một Database sạch.
/// </summary>
public class DatabaseMigrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("MigrationTest123!")
        .Build();

    private readonly ITestOutputHelper _output;

    public DatabaseMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "MigrationCompliance")]
    public async Task AllServiceMigrations_ShouldApplySuccessfully_OnCleanDatabase()
    {
        var connectionString = _dbContainer.GetConnectionString();
        if (!connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = $"{connectionString.TrimEnd(';')};TrustServerCertificate=True;Encrypt=False;";
        }

        _output.WriteLine("Starting Migration Compliance Test...");

        // 1. Admin Service
        await TestMigrationAsync<AdminDbContext>(connectionString, "Bizcore_Admin");

        // 2. Audit Service
        await TestMigrationAsync<AuditDbContext>(connectionString, "Bizcore_Audit");

        // 3. Invoice Service
        await TestMigrationAsync<Invoice.API.Infrastructure.Data.AppDbContext>(connectionString, "Bizcore_Invoice");

        // 4. Payment Service
        await TestMigrationAsync<Payment.API.Infrastructure.Data.AppDbContext>(connectionString, "Bizcore_Payment");

        // 5. Report Service
        await TestMigrationAsync<Report.API.Infrastructure.Data.AppDbContext>(connectionString, "Bizcore_Report");

        // 6. Orchestration Service (Saga)
        await TestMigrationAsync<Orchestration.API.Infrastructure.Data.AppDbContext>(connectionString, "Bizcore_Orchestration");

        // 7. Customer Service (Points/CustomerPointsTransactions)
        await TestMigrationAsync<Customer.API.Infrastructure.Data.AppDbContext>(connectionString, "Bizcore_Customer");

        _output.WriteLine("All migrations applied successfully! ✅");
    }

    private async Task TestMigrationAsync<TContext>(string baseConnectionString, string dbName) where TContext : DbContext
    {
        _output.WriteLine($"Testing migration for: {typeof(TContext).Name} (DB: {dbName})");

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = dbName
        };
        var connectionString = builder.ConnectionString;

        // Đảm bảo DB tồn tại trước khi Migrate
        DatabaseExtensions.PreCreateDatabase(connectionString);

        var services = new ServiceCollection();
        services.AddDbContext<TContext>(options => options.UseSqlServer(connectionString));
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Information));

        var serviceProvider = services.BuildServiceProvider();

        // Thực thi migration
        // Nếu có lỗi SQL hoặc logic, lệnh này sẽ ném ra Exception và làm Test thất bại.
        await serviceProvider.MigrateDatabaseAsync<TContext>();

        // Verify: Kiểm tra xem database thực sự có bảng __EFMigrationsHistory
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        
        appliedMigrations.Should().NotBeEmpty($"{typeof(TContext).Name} should have at least one migration.");
        _output.WriteLine($"Successfully applied {appliedMigrations.Count()} migrations for {dbName}.");
    }
}
