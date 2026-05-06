using Microsoft.EntityFrameworkCore;

namespace Bizcore.UnitTests;

internal static class TestDbContextFactory
{
    public static Invoice.API.Infrastructure.Data.AppDbContext CreateInvoiceDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<Invoice.API.Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new Invoice.API.Infrastructure.Data.AppDbContext(options);
    }

    public static Payment.API.Infrastructure.Data.AppDbContext CreatePaymentDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<Payment.API.Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new Payment.API.Infrastructure.Data.AppDbContext(options);
    }

    public static Report.API.Infrastructure.Data.AppDbContext CreateReportDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<Report.API.Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new Report.API.Infrastructure.Data.AppDbContext(options);
    }

    public static Orchestration.API.Infrastructure.Data.AppDbContext CreateOrchestrationDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<Orchestration.API.Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new Orchestration.API.Infrastructure.Data.AppDbContext(options);
    }
}

