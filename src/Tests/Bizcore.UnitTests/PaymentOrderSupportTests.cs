using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Payment.API.Application.Commands;
using Payment.API.Application.Consumers;
using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using PaymentOrderEntity = Payment.API.Domain.Entities.Order;

namespace Bizcore.UnitTests;

public class PaymentOrderSupportTests
{
    private static InitiatePaymentCommandHandler BuildHandler(
        Payment.API.Infrastructure.Data.AppDbContext context,
        Mock<IPublishEndpoint>? publishMock = null)
    {
        publishMock ??= new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var idempotencyService = new IdempotencyService(context, NullLogger<IdempotencyService>.Instance);

        return new InitiatePaymentCommandHandler(
            context, publishMock.Object, idempotencyService, Mock.Of<IAuditPublisher>(),
            NullLogger<InitiatePaymentCommandHandler>.Instance);
    }

    // ---------- OrderCreatedConsumer (read-model sync) ----------

    [Fact]
    public async Task OrderCreatedConsumer_WhenNew_AddsToReadModel()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var orderId = Guid.NewGuid();
        var message = new OrderCreatedEvent(orderId, Guid.NewGuid(), "Khách", "ORD001", 100m, [], DateTime.UtcNow);

        var consumer = new OrderCreatedConsumer(context, NullLogger<OrderCreatedConsumer>.Instance);
        var ctx = new Mock<ConsumeContext<OrderCreatedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        context.Orders.Should().ContainSingle(o => o.Id == orderId);
    }

    [Fact]
    public async Task OrderCreatedConsumer_WhenAlreadyExists_DoesNotDuplicate()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var orderId = Guid.NewGuid();
        context.Orders.Add(new PaymentOrderEntity { Id = orderId });
        await context.SaveChangesAsync();

        var message = new OrderCreatedEvent(orderId, Guid.NewGuid(), "Khách", "ORD002", 100m, [], DateTime.UtcNow);
        var consumer = new OrderCreatedConsumer(context, NullLogger<OrderCreatedConsumer>.Instance);
        var ctx = new Mock<ConsumeContext<OrderCreatedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        context.Orders.Count(o => o.Id == orderId).Should().Be(1);
    }

    // ---------- InitiatePaymentCommandHandler — Order path ----------

    [Fact]
    public async Task InitiatePaymentHandler_WithOrderId_WhenOrderExists_CreatesPayment_AndPublishesWithOrderId()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var orderId = Guid.NewGuid();
        context.Orders.Add(new PaymentOrderEntity { Id = orderId });
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        IPaymentInitiatedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IPaymentInitiatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IPaymentInitiatedEvent>(m =>
                m.OrderId == (Guid?)values.GetType().GetProperty("OrderId")!.GetValue(values) &&
                m.InvoiceId == (Guid?)values.GetType().GetProperty("InvoiceId")!.GetValue(values)))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(null, orderId, 500m, "CreditCard", "idem-order-1"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        result.Accepted.Should().BeTrue();
        var saved = await context.Payments.SingleAsync(p => p.Id == result.PaymentId);
        saved.OrderId.Should().Be(orderId);
        saved.InvoiceId.Should().BeNull();

        published.Should().NotBeNull();
        published!.OrderId.Should().Be(orderId);
        published.InvoiceId.Should().BeNull();
    }

    [Fact]
    public async Task InitiatePaymentHandler_WithOrderId_WhenOrderMissingInReadModel_Rejects()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(null, Guid.NewGuid(), 500m, "CreditCard", "idem-order-2"),
            CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.ErrorReason.Should().Be("Order not found.");
        context.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePaymentHandler_WhenNeitherInvoiceNorOrderProvided_Rejects()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(null, null, 500m, "CreditCard", "idem-neither"),
            CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.ErrorReason.Should().Contain("Exactly one");
    }

    [Fact]
    public async Task InitiatePaymentHandler_WhenBothInvoiceAndOrderProvided_Rejects()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = BuildHandler(context, publishMock);

        var result = await handler.Handle(
            new InitiatePaymentCommand(Guid.NewGuid(), Guid.NewGuid(), 500m, "CreditCard", "idem-both"),
            CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.ErrorReason.Should().Contain("Exactly one");
    }
}
