using System;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Report.API.Application.Services;
using ReportInvoiceEntity = Report.API.Domain.Entities.Invoice;

namespace Bizcore.UnitTests;

public class ReportServiceTests
{
    [Fact]
    public async Task GetDashboardStatsAsync_WhenCacheMiss_ComputesAndCaches()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateReportDbContext(dbName);

        context.Invoices.AddRange(
            new ReportInvoiceEntity
            {
                Id = Guid.NewGuid(),
                CustomerName = "A",
                Amount = 100m,
                Status = InvoiceStatus.Paid,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ReportInvoiceEntity
            {
                Id = Guid.NewGuid(),
                CustomerName = "B",
                Amount = 50m,
                Status = InvoiceStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );
        await context.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ReportService(context, cache);

        var first = await service.GetDashboardStatsAsync();
        first.TotalInvoices.Should().Be(2);
        first.TotalRevenue.Should().Be(150m);
        first.PaidInvoices.Should().Be(1);
        first.PendingInvoices.Should().Be(1);

        // Update DB after first call; service should return cached values.
        context.Invoices.Add(new ReportInvoiceEntity
        {
            Id = Guid.NewGuid(),
            CustomerName = "C",
            Amount = 999m,
            Status = InvoiceStatus.Paid,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var second = await service.GetDashboardStatsAsync();

        second.TotalInvoices.Should().Be(2);
        second.TotalRevenue.Should().Be(150m);
        second.PaidInvoices.Should().Be(1);
        second.PendingInvoices.Should().Be(1);
    }
}

