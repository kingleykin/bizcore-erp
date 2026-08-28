using System;
using System.Linq;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Application.Consumers;
using Payment.API.Application.Hubs;
using Payment.API.Domain.Entities;
using PaymentEntity = Payment.API.Domain.Entities.Payment;
using PaymentInvoiceEntity = Payment.API.Domain.Entities.Invoice;
using Payment.API.Infrastructure.Telemetry;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class PaymentCompensationRequestedConsumerTests
{
    private static IHubContext<PaymentHub> BuildHubContextMock()
    {
        var hubContextMock = new Mock<IHubContext<PaymentHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        return hubContextMock.Object;
    }

    private sealed class PaymentCompensationRequestedEventFake : IPaymentCompensationRequestedEvent
    {
        public PaymentCompensationRequestedEventFake(Guid paymentId, Guid invoiceId, decimal amount, string reason, Guid? orderId = null)
        {
            PaymentId = paymentId;
            OrderId = orderId;
            InvoiceId = invoiceId;
            Amount = amount;
            Reason = reason;
            RequestedAt = DateTime.UtcNow;
        }

        public Guid PaymentId { get; }
        public Guid? OrderId { get; }
        public Guid? InvoiceId { get; }
        public decimal Amount { get; }
        public DateTime RequestedAt { get; }
        public string Reason { get; }
    }

    [Fact]
    public async Task Consume_WhenPaymentExists_MarksAsReversed()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

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

        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("Bizcore.Payment");
        meterFactoryMock.Setup(m => m.Create(It.IsAny<MeterOptions>())).Returns(meter);
        var metrics = new PaymentMetrics(meterFactoryMock.Object);

        var consumer = new PaymentCompensationRequestedConsumer(context, metrics, BuildHubContextMock(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompensationRequestedEventFake(payment.Id, invoiceId, 300m, "invoice update failed"));

        await consumer.Consume(consumeContext.Object);

        context.Payments.Single(p => p.Id == payment.Id).Status.Should().Be(PaymentStatus.Reversed);
    }

    [Fact]
    public async Task Consume_WhenPaymentExists_PushesReversedStatusViaSignalR()
    {
        // Regression: khách đã thấy toast "thanh toán thành công" (Payment.Completed) từ trước qua
        // SignalR — nếu bồi hoàn xảy ra sau đó mà không đẩy lại real-time, khách sẽ không bao giờ
        // biết giao dịch vừa bị hoàn cho tới khi tự làm mới trang.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

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

        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("Bizcore.Payment");
        meterFactoryMock.Setup(m => m.Create(It.IsAny<MeterOptions>())).Returns(meter);
        var metrics = new PaymentMetrics(meterFactoryMock.Object);

        var hubContextMock = new Mock<IHubContext<PaymentHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(payment.Id.ToString())).Returns(clientProxyMock.Object);

        var consumer = new PaymentCompensationRequestedConsumer(context, metrics, hubContextMock.Object, NullLogger<PaymentCompensationRequestedConsumer>.Instance);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompensationRequestedEventFake(payment.Id, invoiceId, 300m, "cộng điểm thất bại vĩnh viễn"));

        await consumer.Consume(consumeContext.Object);

        clientProxyMock.Verify(p => p.SendCoreAsync(
            "PaymentStatusUpdated",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenPaymentMissing_DoesNothing()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("Bizcore.Payment");
        meterFactoryMock.Setup(m => m.Create(It.IsAny<MeterOptions>())).Returns(meter);
        var metrics = new PaymentMetrics(meterFactoryMock.Object);

        var consumer = new PaymentCompensationRequestedConsumer(context, metrics, BuildHubContextMock(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);

        var consumeContext = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new PaymentCompensationRequestedEventFake(Guid.NewGuid(), Guid.NewGuid(), 99m, "invoice missing"));

        await consumer.Consume(consumeContext.Object);

        context.Payments.Should().BeEmpty();
    }
}

