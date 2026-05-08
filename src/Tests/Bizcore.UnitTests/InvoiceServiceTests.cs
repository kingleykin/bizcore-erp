using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using Invoice.API.Application.Services;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using Moq;

namespace Bizcore.UnitTests;

public class InvoiceServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAllInvoices()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice1 = InvoiceEntity.Create("Alice", 123_000m);
        var invoice2 = InvoiceEntity.Create("Bob", 50_000m);
        await context.Invoices.AddRangeAsync(invoice1, invoice2);
        await context.SaveChangesAsync();

        var service = new InvoiceService(context);

        var result = await service.GetAllAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.CustomerName == "Alice");
        result.Should().Contain(i => i.CustomerName == "Bob");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectInvoice()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice = InvoiceEntity.Create("Alice", 123_000m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var service = new InvoiceService(context);

        var result = await service.GetByIdAsync(invoice.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(invoice.Id);
        result.CustomerName.Should().Be("Alice");
    }
}
