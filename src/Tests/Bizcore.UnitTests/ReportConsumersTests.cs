using System;
using System.Linq;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Moq;
using Report.API.Application.Consumers;
using ReportInvoiceEntity = Report.API.Domain.Entities.Invoice;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class ReportConsumersTests
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

    private sealed class PaymentCompletedEventFake : IPaymentCompletedEvent
    {
        public PaymentCompletedEventFake(Guid paymentId, Guid invoiceId, decimal amount, DateTime paymentDate)
        {
            PaymentId = paymentId;
            InvoiceId = invoiceId;
            Amount = amount;
            PaymentDate = paymentDate;
        }

        public Guid PaymentId { get; }
        public Guid InvoiceId { get; }
        public decimal Amount { get; }
        public DateTime PaymentDate { get; }
    }

    [Fact]
    public async Task InvoiceCreatedConsumer_Consume_AddsPendingInvoice()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateReportDbContext(connection);

        var consumer = new InvoiceCreatedConsumer(context);

        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var message = new InvoiceCreatedEventFake(id, "Cust", 123m, createdAt);

        var consumeContext = new Mock<ConsumeContext<IInvoiceCreatedEvent>>();
        consumeContext.SetupGet(x => x.Message).Returns(message);

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Count().Should().Be(1);
        var saved = context.Invoices.Single();

        saved.Id.Should().Be(id);
        saved.CustomerName.Should().Be("Cust");
        saved.Amount.Should().Be(123m);
        saved.Status.Should().Be(InvoiceStatus.Pending);
        saved.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task PaymentCompletedConsumer_Consume_UpdatesInvoiceToPaid()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateReportDbContext(connection);

        var invoiceId = Guid.NewGuid();
        var invoice = new ReportInvoiceEntity
        {
            Id = invoiceId,
            CustomerName = "Cust",
            Amount = 50m,
            Status = InvoiceStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var consumer = new PaymentCompletedConsumer(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentCompletedConsumer>.Instance);

        var message = new PaymentCompletedEventFake(Guid.NewGuid(), invoiceId, 50m, DateTime.UtcNow);
        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext.SetupGet(x => x.Message).Returns(message);

        await consumer.Consume(consumeContext.Object);

        var saved = context.Invoices.Single(i => i.Id == invoiceId);
        saved.Status.Should().Be(InvoiceStatus.Paid);
    }
}

