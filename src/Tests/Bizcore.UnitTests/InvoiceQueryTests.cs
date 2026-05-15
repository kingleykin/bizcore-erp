using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using Invoice.API.Application.Queries;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using Xunit;
using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class InvoiceQueryTests
{
    [Fact]
    public async Task GetInvoicesHandler_ReturnsAllInvoices()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);

        var invoice1 = InvoiceEntity.Create("Alice", 123_000m);
        var invoice2 = InvoiceEntity.Create("Bob", 50_000m);
        await context.Invoices.AddRangeAsync(invoice1, invoice2);
        await context.SaveChangesAsync();

        var handler = new GetInvoicesHandler(context);

        var result = await handler.Handle(new GetInvoicesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.CustomerName == "Alice");
        result.Should().Contain(i => i.CustomerName == "Bob");
    }

    [Fact]
    public async Task GetInvoiceByIdHandler_ReturnsCorrectInvoice()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);

        var invoice = InvoiceEntity.Create("Alice", 123_000m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var handler = new GetInvoiceByIdHandler(context);

        var result = await handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(invoice.Id);
        result.CustomerName.Should().Be("Alice");
    }
}
