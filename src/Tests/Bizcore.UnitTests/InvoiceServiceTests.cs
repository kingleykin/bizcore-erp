using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using Invoice.API.Application.Services;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using MassTransit;
using Moq;

namespace Bizcore.UnitTests;

public class InvoiceServiceTests
{
    [Fact]
    public async Task CreateAsync_AddsInvoice_PublishesInvoiceCreatedEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var publishedValues = (object?)null;
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        publishMock
            .Setup(p => p.Publish<IInvoiceCreatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => publishedValues = values)
            .Returns(Task.CompletedTask);

        var service = new InvoiceService(context, publishMock.Object);

        var invoice = InvoiceEntity.Create("Alice", 123_000m);

        var result = await service.CreateAsync(invoice);

        result.Should().BeEquivalentTo(invoice);

        var saved = context.Invoices.Single(i => i.Id == invoice.Id);
        saved.CustomerName.Should().Be("Alice");
        saved.Amount.Should().Be(123_000m);
        saved.Status.Should().Be(InvoiceStatus.Pending);
        saved.CreatedAt.Should().Be(invoice.CreatedAt);

        publishMock.Verify(
            p => p.Publish<IInvoiceCreatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishedValues.Should().NotBeNull();

        var publishedType = publishedValues!.GetType();
        publishedType.GetProperty("Id")!.GetValue(publishedValues).Should().Be(invoice.Id);
        publishedType.GetProperty("CustomerName")!.GetValue(publishedValues).Should().Be("Alice");
        publishedType.GetProperty("Amount")!.GetValue(publishedValues).Should().Be(123_000m);
        publishedType.GetProperty("CreatedAt")!.GetValue(publishedValues).Should().Be(invoice.CreatedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvoiceMissing_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Loose);
        var service = new InvoiceService(context, publishMock.Object);

        var ok = await service.UpdateStatusAsync(Guid.NewGuid(), InvoiceStatus.Paid);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInvoiceExists_UpdatesStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Loose);
        var service = new InvoiceService(context, publishMock.Object);

        var invoice = InvoiceEntity.Create("Bob", 50_000m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var ok = await service.UpdateStatusAsync(invoice.Id, InvoiceStatus.Paid);

        ok.Should().BeTrue();
        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Paid);
    }
}

