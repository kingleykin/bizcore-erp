using System;
using System.Linq;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Application.Consumers;
using Payment.API.Domain.Entities;
using PaymentEntity = Payment.API.Domain.Entities.Payment;
using PaymentInvoiceEntity = Payment.API.Domain.Entities.Invoice;

namespace Bizcore.UnitTests;

public class PaymentCompensationRequestedConsumerTests
{
    private sealed class PaymentCompensationRequestedEventFake : IPaymentCompensationRequestedEvent
    {
        public PaymentCompensationRequestedEventFake(Guid paymentId, Guid invoiceId, decimal amount, string reason)
        {
            PaymentId = paymentId;
            InvoiceId = invoiceId;
            Amount = amount;
            Reason = reason;
            RequestedAt = DateTime.UtcNow;
        }

        public Guid PaymentId { get; }
        public Guid InvoiceId { get; }
        public decimal Amount { get; }
        public DateTime RequestedAt { get; }
        public string Reason { get; }
    }

    [Fact]
    public async Task Consume_WhenPaymentExists_MarksAsReversed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId });

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Amount = 300m,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Completed
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<PaymentCompensationRequestedConsumer>>();
        var consumer = new PaymentCompensationRequestedConsumer(context, loggerMock.Object);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompensationRequestedEventFake(payment.Id, invoiceId, 300m, "invoice update failed"));

        await consumer.Consume(consumeContext.Object);

        context.Payments.Single(p => p.Id == payment.Id).Status.Should().Be(PaymentStatus.Reversed);
    }

    [Fact]
    public async Task Consume_WhenPaymentMissing_DoesNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var loggerMock = new Mock<ILogger<PaymentCompensationRequestedConsumer>>();
        var consumer = new PaymentCompensationRequestedConsumer(context, loggerMock.Object);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompensationRequestedEventFake(Guid.NewGuid(), Guid.NewGuid(), 99m, "invoice missing"));

        await consumer.Consume(consumeContext.Object);

        context.Payments.Should().BeEmpty();
    }
}

