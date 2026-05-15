using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

namespace Bizcore.UnitTests;

public class PaymentServiceTests
{
    private static PaymentService BuildService(
        AppDbContext context,
        Mock<IPublishEndpoint>? publishMock = null,
        IIdempotencyService? idempotencyService = null)
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

        return new PaymentService(
            context,
            publishMock.Object,
            idempotencyService,
            NullLogger<PaymentService>.Instance);
    }

    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        return context;
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyEmpty_ReturnsRejected_AndDoesNotPersistOrPublish()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var service = BuildService(context, publishMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = Guid.NewGuid(), Amount = 10_000m },
            "");

        result.Accepted.Should().BeFalse();
        result.PaymentId.Should().BeNull();
        result.ErrorReason.Should().Be("Idempotency key is required.");
        context.Payments.Should().BeEmpty();
        context.IdempotencyRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceMissingInReadModel_ReturnsRejected_AndDoesNotPersistOrPublish()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var service = BuildService(context, publishMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = Guid.NewGuid(), Amount = 99_000m },
            "idem-no-invoice");

        result.Accepted.Should().BeFalse();
        result.PaymentId.Should().BeNull();
        result.ErrorReason.Should().Be("Invoice not found.");
        context.Payments.Should().BeEmpty();
        context.IdempotencyRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenAccepted_CreatesProcessingPayment_IdempotencyRecord_AndPublishesInitiatedEvent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var invoiceId = Guid.NewGuid();
        var idempotencyKey = "idem-async-success";
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = BuildService(context, publishMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 10_500m },
            idempotencyKey);

        result.Accepted.Should().BeTrue();
        result.PaymentId.Should().NotBeNull();
        result.ErrorReason.Should().BeNull();

        var saved = context.Payments.Single(p => p.Id == result.PaymentId);
        saved.InvoiceId.Should().Be(invoiceId);
        saved.Amount.Should().Be(10_500m);
        saved.Status.Should().Be(PaymentStatus.Processing);
        saved.IdempotencyKey.Should().Be(idempotencyKey);
        saved.PaymentDate.Should().NotBe(default);

        var idempotencyRecord = context.IdempotencyRecords.Single(r => r.Key == idempotencyKey);
        idempotencyRecord.PaymentId.Should().Be(saved.Id);
        idempotencyRecord.RequestHash.Should().NotBeNullOrWhiteSpace();

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
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyExists_ReturnsExistingPayment_WithoutCreatingOrPublishingAgain()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var invoiceId = Guid.NewGuid();
        var idempotencyKey = "idem-duplicate";
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = BuildService(context, publishMock);

        var first = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 25_000m },
            idempotencyKey);
        var duplicate = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 25_000m },
            idempotencyKey);

        duplicate.Accepted.Should().BeTrue();
        duplicate.PaymentId.Should().Be(first.PaymentId);
        context.Payments.Should().ContainSingle();
        context.IdempotencyRecords.Should().ContainSingle(r => r.Key == idempotencyKey);

        publishMock.Verify(
            p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenSameIdempotencyKeyHasDifferentPayload_ReturnsConflict()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var invoiceId = Guid.NewGuid();
        var idempotencyKey = "idem-conflict";
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = BuildService(context, publishMock);

        await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 25_000m },
            idempotencyKey);

        var conflict = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 30_000m },
            idempotencyKey);

        conflict.Accepted.Should().BeFalse();
        conflict.PaymentId.Should().BeNull();
        conflict.ErrorReason.Should().Be("Idempotency key already used with different request payload");
        context.Payments.Should().ContainSingle();
        publishMock.Verify(
            p => p.Publish<IPaymentInitiatedEvent>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentConsumer_WhenPaymentIsProcessing_MarksCompleted_AndPublishesFinalEvents()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
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

        context.Payments.Single().Status.Should().Be(PaymentStatus.Completed);
        publishMock.Verify(
            p => p.Publish<IPaymentConfirmedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishMock.Verify(
            p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentConsumer_WhenPaymentAlreadyCompleted_IsIdempotent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            Amount = 12_000m,
            Status = PaymentStatus.Completed
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var command = new Mock<IConfirmPaymentCommand>();
        command.SetupGet(c => c.PaymentId).Returns(payment.Id);
        command.SetupGet(c => c.InvoiceId).Returns(payment.InvoiceId);

        var hubContextMock = new Mock<IHubContext<PaymentHub>>();

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

        context.Payments.Single().Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task RejectPaymentConsumer_WhenPaymentIsProcessing_MarksFailed_AndPublishesRejectedEvent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            Amount = 12_000m,
            Status = PaymentStatus.Processing
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentRejectedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new Mock<IRejectPaymentCommand>();
        command.SetupGet(c => c.PaymentId).Returns(payment.Id);
        command.SetupGet(c => c.InvoiceId).Returns(payment.InvoiceId);
        command.SetupGet(c => c.Reason).Returns("Invoice validation failed.");

        var hubContextMock = new Mock<IHubContext<PaymentHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var consumer = new RejectPaymentConsumer(
            context,
            publishMock.Object,
            hubContextMock.Object,
            NullLogger<RejectPaymentConsumer>.Instance);

        await consumer.Consume(BuildConsumeContext(command.Object).Object);

        var saved = context.Payments.Single();
        saved.Status.Should().Be(PaymentStatus.Failed);
        saved.FailureReason.Should().Be("Invoice validation failed.");
        publishMock.Verify(
            p => p.Publish<IPaymentRejectedEvent>(
                It.Is<object>(message =>
                    Get<Guid>(message, nameof(IPaymentRejectedEvent.PaymentId)) == payment.Id
                    && Get<Guid>(message, nameof(IPaymentRejectedEvent.InvoiceId)) == payment.InvoiceId
                    && Get<string>(message, nameof(IPaymentRejectedEvent.Reason)) == "Invoice validation failed."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectPaymentConsumer_WhenPaymentAlreadyFailed_IsIdempotent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            Amount = 12_000m,
            Status = PaymentStatus.Failed,
            FailureReason = "Already failed."
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var command = new Mock<IRejectPaymentCommand>();
        command.SetupGet(c => c.PaymentId).Returns(payment.Id);
        command.SetupGet(c => c.InvoiceId).Returns(payment.InvoiceId);
        command.SetupGet(c => c.Reason).Returns("Duplicate failure.");

        var hubContextMock = new Mock<IHubContext<PaymentHub>>();

        var consumer = new RejectPaymentConsumer(
            context,
            publishMock.Object,
            hubContextMock.Object,
            NullLogger<RejectPaymentConsumer>.Instance);

        await consumer.Consume(BuildConsumeContext(command.Object).Object);

        var saved = context.Payments.Single();
        saved.Status.Should().Be(PaymentStatus.Failed);
        saved.FailureReason.Should().Be("Already failed.");
    }

    private static T Get<T>(object message, string propertyName)
    {
        var property = message.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"published message should include {propertyName}");
        return ((T?)property!.GetValue(message))!;
    }
}
