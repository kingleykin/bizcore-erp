using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using Invoice.API.Application.Consumers;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bizcore.UnitTests;

public class InvoicePaymentCompletedConsumerTests
{
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
    public async Task Consume_WhenInvoiceExists_UpdatesToPaid_AndDoesNotPublishCompensation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice = InvoiceEntity.Create("Comp", 1_000m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<PaymentCompletedConsumer>>();
        var consumer = new PaymentCompletedConsumer(context, publishMock.Object, loggerMock.Object);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompletedEventFake(Guid.NewGuid(), invoice.Id, invoice.Amount, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Paid);
        publishMock.Verify(
            p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenInvoiceMissing_PublishesCompensationRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        object? publishedPayload = null;

        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => publishedPayload = payload)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<PaymentCompletedConsumer>>();
        var consumer = new PaymentCompletedConsumer(context, publishMock.Object, loggerMock.Object);

        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var amount = 200m;

        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompletedEventFake(paymentId, invoiceId, amount, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        publishMock.Verify(
            p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishedPayload.Should().NotBeNull();

        var payloadType = publishedPayload!.GetType();
        payloadType.GetProperty("PaymentId")!.GetValue(publishedPayload).Should().Be(paymentId);
        payloadType.GetProperty("InvoiceId")!.GetValue(publishedPayload).Should().Be(invoiceId);
        payloadType.GetProperty("Amount")!.GetValue(publishedPayload).Should().Be(amount);
        payloadType.GetProperty("Reason")!.GetValue(publishedPayload).Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_WhenAmountMismatch_PublishesCompensation_AndLeavesInvoicePending()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice = InvoiceEntity.Create("Mismatch", 500m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new PaymentCompletedConsumer(context, publishMock.Object, Mock.Of<ILogger<PaymentCompletedConsumer>>());

        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompletedEventFake(Guid.NewGuid(), invoice.Id, 999m, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Pending);
        publishMock.Verify(
            p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenInvoiceAlreadyPaid_PublishesCompensation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice = InvoiceEntity.Create("Paid", 300m);
        invoice.Status = InvoiceStatus.Paid;
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new PaymentCompletedConsumer(context, publishMock.Object, Mock.Of<ILogger<PaymentCompletedConsumer>>());

        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompletedEventFake(Guid.NewGuid(), invoice.Id, invoice.Amount, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        publishMock.Verify(
            p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenInvoiceCancelled_PublishesCompensation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);

        var invoice = InvoiceEntity.Create("Cancelled", 150m);
        invoice.Status = InvoiceStatus.Cancelled;
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new PaymentCompletedConsumer(context, publishMock.Object, Mock.Of<ILogger<PaymentCompletedConsumer>>());

        var consumeContext = new Mock<ConsumeContext<IPaymentCompletedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompletedEventFake(Guid.NewGuid(), invoice.Id, invoice.Amount, DateTime.UtcNow));

        await consumer.Consume(consumeContext.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Cancelled);
        publishMock.Verify(
            p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

