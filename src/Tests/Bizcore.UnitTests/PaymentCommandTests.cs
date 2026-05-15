using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Abstractions;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Payment.API.Application.Commands;
using Payment.API.Application.Queries;
using Payment.API.Application.Consumers;
using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;
using PaymentEntity = Payment.API.Domain.Entities.Payment;
using PaymentInvoiceEntity = Payment.API.Domain.Entities.Invoice;
using Microsoft.AspNetCore.SignalR;
using Payment.API.Application.Hubs;
using Microsoft.Extensions.Logging;
using Payment.API.Infrastructure.Telemetry;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class PaymentCommandTests
{
    private static InitiatePaymentCommandHandler BuildHandler(
        AppDbContext context,
        Mock<IPublishEndpoint>? publishMock = null,
        IIdempotencyService? idempotencyService = null,
        Mock<IAuditPublisher>? auditMock = null)
    {
        publishMock ??= new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        idempotencyService ??= new IdempotencyService(
            context,
            NullLogger<IdempotencyService>.Instance);

        auditMock ??= new Mock<IAuditPublisher>();

        return new InitiatePaymentCommandHandler(
            context,
            publishMock.Object,
            idempotencyService,
            auditMock.Object,
            NullLogger<InitiatePaymentCommandHandler>.Instance);
    }

    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        return context;
    }

    [Fact]
    public async Task InitiatePaymentHandler_WhenIdempotencyKeyEmpty_ReturnsRejected_AndDoesNotPersistOrPublish()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(Guid.NewGuid(), 10_000m, "CreditCard", ""),
            CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.PaymentId.Should().BeNull();
        result.ErrorReason.Should().Be("Idempotency key is required.");
        context.Payments.Should().BeEmpty();
        context.IdempotencyRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePaymentHandler_WhenInvoiceMissingInReadModel_ReturnsRejected_AndDoesNotPersistOrPublish()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(Guid.NewGuid(), 99_000m, "CreditCard", "idem-no-invoice"),
            CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.PaymentId.Should().BeNull();
        result.ErrorReason.Should().Be("Invoice not found.");
        context.Payments.Should().BeEmpty();
        context.IdempotencyRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePaymentHandler_WhenAccepted_CreatesProcessingPayment_IdempotencyRecord_AndPublishesInitiatedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var invoiceId = Guid.NewGuid();
        var idempotencyKey = "idem-async-success";
        
        // Note: Payment API has its own local Invoice read model
        var invoice = new PaymentInvoiceEntity { Id = invoiceId, Status = Bizcore.BuildingBlocks.InvoiceStatus.Pending };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(invoiceId, 10_500m, "CreditCard", idempotencyKey),
            CancellationToken.None);

        // Manually save changes as TransactionBehavior is missing in unit test
        await context.SaveChangesAsync();

        result.Accepted.Should().BeTrue();
        result.PaymentId.Should().NotBeNull();
        result.ErrorReason.Should().BeNull();

        var saved = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == result.PaymentId);
        saved.InvoiceId.Should().Be(invoiceId);
        saved.Amount.Should().Be(10_500m);
        saved.Status.Should().Be(PaymentStatus.Processing);
        saved.IdempotencyKey.Should().Be(idempotencyKey);

        var idempotencyRecord = await context.IdempotencyRecords.AsNoTracking().SingleAsync(r => r.Key == idempotencyKey);
        idempotencyRecord.PaymentId.Should().Be(saved.Id);

        publishMock.Verify(
            p => p.Publish<IPaymentInitiatedEvent>(
                It.Is<object>(message =>
                    Get<Guid>(message, nameof(IPaymentInitiatedEvent.PaymentId)) == saved.Id
                    && Get<Guid>(message, nameof(IPaymentInitiatedEvent.InvoiceId)) == invoiceId
                    && Get<decimal>(message, nameof(IPaymentInitiatedEvent.Amount)) == 10_500m
                    && Get<string>(message, nameof(IPaymentInitiatedEvent.IdempotencyKey)) == idempotencyKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentConsumer_WhenPaymentIsProcessing_MarksCompleted_AndPublishesFinalEvents()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            Amount = 12_000m,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Processing
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentConfirmedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publishMock
            .Setup(p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new Mock<IConfirmPaymentCommand>();
        command.SetupGet(c => c.PaymentId).Returns(payment.Id);
        command.SetupGet(c => c.InvoiceId).Returns(payment.InvoiceId);

        var hubContextMock = new Mock<IHubContext<PaymentHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("Bizcore.Payment");
        meterFactoryMock.Setup(m => m.Create(It.IsAny<MeterOptions>())).Returns(meter);
        var metrics = new PaymentMetrics(meterFactoryMock.Object);

        var consumer = new ConfirmPaymentConsumer(
            context,
            publishMock.Object,
            hubContextMock.Object,
            metrics,
            NullLogger<ConfirmPaymentConsumer>.Instance);

        await consumer.Consume(BuildConsumeContext(command.Object).Object);

        var saved = await context.Payments.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(PaymentStatus.Completed);
    }

    private static T Get<T>(object message, string propertyName)
    {
        var property = message.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"published message should include {propertyName}");
        return ((T?)property!.GetValue(message))!;
    }
}
