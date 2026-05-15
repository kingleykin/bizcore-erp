using System;
using System.Linq;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Application.Consumers;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class PaymentInvoiceCreatedConsumerTests
{
    private sealed class InvoiceCreatedEventFake : IInvoiceCreatedEvent
    {
        public InvoiceCreatedEventFake(Guid id, string customerName, decimal amount, DateTime createdAt)
        {
            Id = id;
            CustomerName = customerName;
            Amount = amount;
            CreatedAt = createdAt;
        }

        public Guid Id { get; }
        public string CustomerName { get; }
        public decimal Amount { get; }
        public DateTime CreatedAt { get; }
    }

    [Fact]
    public async Task Consume_WhenInvoiceMissing_AddsInvoiceToPaymentReadModel()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var consumer = new InvoiceCreatedConsumer(context, Mock.Of<ILogger<InvoiceCreatedConsumer>>());
        var invoiceId = Guid.NewGuid();

        var consumeContext = new Mock<ConsumeContext<IInvoiceCreatedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new InvoiceCreatedEventFake(invoiceId, "Cust", 100m, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Count().Should().Be(1);
        var saved = context.Invoices.Single();
        saved.Id.Should().Be(invoiceId);
        saved.Status.Should().Be(InvoiceStatus.Pending);
    }

    [Fact]
    public async Task Consume_WhenInvoiceAlreadyExists_DoesNotInsertDuplicate()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var invoiceId = Guid.NewGuid();
        var invoice = new Payment.API.Domain.Entities.Invoice { Id = invoiceId, Status = InvoiceStatus.Pending };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var consumer = new InvoiceCreatedConsumer(context, Mock.Of<ILogger<InvoiceCreatedConsumer>>());

        var consumeContext = new Mock<ConsumeContext<IInvoiceCreatedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new InvoiceCreatedEventFake(invoiceId, "Cust", 100m, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Count(i => i.Id == invoiceId).Should().Be(1);
    }
}

