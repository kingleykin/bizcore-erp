using System;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Report.API.Application.Queries;
using Report.API.Domain.Entities;
using Xunit;
using ReportInvoiceEntity = Report.API.Domain.Entities.Invoice;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class ReportQueryTests
{
    [Fact]
    public async Task GetDashboardStatsHandler_WhenCacheMiss_ComputesAndCaches()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateReportDbContext(connection);

        var inv1 = new ReportInvoiceEntity
        {
            Id = Guid.NewGuid(),
            CustomerName = "A",
            Amount = 100m,
            Status = InvoiceStatus.Paid,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var inv2 = new ReportInvoiceEntity
        {
            Id = Guid.NewGuid(),
            CustomerName = "B",
            Amount = 50m,
            Status = InvoiceStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        context.Invoices.AddRange(inv1, inv2);
        await context.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetDashboardStatsHandler(context, cache);

        var first = await handler.Handle(new GetDashboardStatsQuery(), CancellationToken.None);
        first.TotalInvoices.Should().Be(2);
        first.TotalRevenue.Should().Be(150m);
        first.PaidInvoices.Should().Be(1);
        first.PendingInvoices.Should().Be(1);

        // Update DB after first call; handler should return cached values.
        var inv3 = new ReportInvoiceEntity
        {
            Id = Guid.NewGuid(),
            CustomerName = "C",
            Amount = 999m,
            Status = InvoiceStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(inv3);
        await context.SaveChangesAsync();

        var second = await handler.Handle(new GetDashboardStatsQuery(), CancellationToken.None);

        second.TotalInvoices.Should().Be(2);
        second.TotalRevenue.Should().Be(150m);
        second.PaidInvoices.Should().Be(1);
        second.PendingInvoices.Should().Be(1);
    }
}
