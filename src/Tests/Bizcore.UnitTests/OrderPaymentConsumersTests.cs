using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Order.API.Application.Consumers;
using OrderEntity = Order.API.Domain.Entities.Order;

namespace Bizcore.UnitTests;

public class OrderPaymentConsumersTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    // ---------- ValidateOrderCommandConsumer ----------

    [Fact]
    public async Task ValidateOrderCommandConsumer_WhenOrderPendingAndAmountMatches_PublishesOrderValidatedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách A", null, [(Guid.NewGuid(), "SP", 2, 50m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        IOrderValidatedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IOrderValidatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IOrderValidatedEvent>(m =>
                m.PaymentId == (Guid)values.GetType().GetProperty("PaymentId")!.GetValue(values)! &&
                m.OrderId == (Guid)values.GetType().GetProperty("OrderId")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var paymentId = Guid.NewGuid();
        var message = Mock.Of<IValidateOrderCommand>(m => m.PaymentId == paymentId && m.OrderId == order.Id && m.Amount == 100m);

        var consumer = new ValidateOrderCommandConsumer(context, publishMock.Object, NullLogger<ValidateOrderCommandConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        published!.PaymentId.Should().Be(paymentId);
        published.OrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task ValidateOrderCommandConsumer_WhenOrderMissing_PublishesValidationFailed_WithNotFoundReason()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var publishMock = new Mock<IPublishEndpoint>();
        IOrderValidationFailedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IOrderValidationFailedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IOrderValidationFailedEvent>(m =>
                m.Reason == (string)values.GetType().GetProperty("Reason")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var message = Mock.Of<IValidateOrderCommand>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == Guid.NewGuid() && m.Amount == 100m);

        var consumer = new ValidateOrderCommandConsumer(context, publishMock.Object, NullLogger<ValidateOrderCommandConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        published!.Reason.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateOrderCommandConsumer_WhenOrderAlreadyConfirmed_PublishesValidationFailed()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách B", null, [(Guid.NewGuid(), "SP", 1, 50m)]);
        order.Confirm();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        IOrderValidationFailedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IOrderValidationFailedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IOrderValidationFailedEvent>(m =>
                m.Reason == (string)values.GetType().GetProperty("Reason")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var message = Mock.Of<IValidateOrderCommand>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Amount == 50m);

        var consumer = new ValidateOrderCommandConsumer(context, publishMock.Object, NullLogger<ValidateOrderCommandConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        published!.Reason.Should().Contain("already confirmed");
    }

    [Fact]
    public async Task ValidateOrderCommandConsumer_WhenOrderCancelled_PublishesValidationFailed()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách C", null, [(Guid.NewGuid(), "SP", 1, 50m)]);
        order.Cancel("khách hủy");
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        IOrderValidationFailedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IOrderValidationFailedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IOrderValidationFailedEvent>(m =>
                m.Reason == (string)values.GetType().GetProperty("Reason")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var message = Mock.Of<IValidateOrderCommand>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Amount == 50m);

        var consumer = new ValidateOrderCommandConsumer(context, publishMock.Object, NullLogger<ValidateOrderCommandConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        published!.Reason.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ValidateOrderCommandConsumer_WhenAmountMismatch_PublishesValidationFailed()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách D", null, [(Guid.NewGuid(), "SP", 1, 50m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        IOrderValidationFailedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IOrderValidationFailedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IOrderValidationFailedEvent>(m =>
                m.Reason == (string)values.GetType().GetProperty("Reason")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        // order.TotalAmount == 50m, nhưng payment yêu cầu validate 999m
        var message = Mock.Of<IValidateOrderCommand>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Amount == 999m);

        var consumer = new ValidateOrderCommandConsumer(context, publishMock.Object, NullLogger<ValidateOrderCommandConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        published!.Reason.Should().Contain("mismatch");
    }

    // ---------- PaymentConfirmedConsumer ----------

    // Regression: PaymentConfirmedConsumer TUYỆT ĐỐI không được đi qua MediatR/ConfirmOrderCommand
    // (ITransactionalCommand) — MassTransit đã tự bọc Consume() trong 1 transaction sẵn
    // (Transactional Inbox), TransactionBehavior mở thêm 1 transaction nữa trên cùng connection
    // sẽ throw "The connection is already in a transaction". Test dưới dùng thẳng AppDbContext,
    // đúng thiết kế thật của consumer.

    [Fact]
    public async Task PaymentConfirmedConsumer_WhenOrderIdIsNull_DoesNothing()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var message = Mock.Of<IPaymentConfirmedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == (Guid?)null && m.InvoiceId == Guid.NewGuid());

        var consumer = new PaymentConfirmedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentConfirmedConsumer>.Instance);

        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task PaymentConfirmedConsumer_WhenOrderPending_ConfirmsOrder_AndPublishesOrderConfirmedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [(productId, "SP", 2, 50m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        OrderConfirmedEvent? published = null;
        publishMock
            .Setup(p => p.Publish(It.IsAny<OrderConfirmedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderConfirmedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var message = Mock.Of<IPaymentConfirmedEvent>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id);

        var consumer = new PaymentConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<PaymentConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var saved = await context.Orders.SingleAsync(o => o.Id == order.Id);
        saved.Status.Should().Be(Bizcore.BuildingBlocks.OrderStatus.Confirmed);

        published.Should().NotBeNull();
        published!.Id.Should().Be(order.Id);
        published.Items.Single().ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task PaymentConfirmedConsumer_WhenOrderAlreadyCancelled_SwallowsDomainException_DoesNotThrow_OrPublish()
    {
        // Race hiếm: đơn bị Hủy tay ngay trong lúc chờ thanh toán — không phải lỗi thoáng qua nên
        // không được throw để retry vô ích, chỉ log lại.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [(Guid.NewGuid(), "SP", 1, 50m)]);
        order.Cancel("khách hủy");
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var message = Mock.Of<IPaymentConfirmedEvent>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id);

        var consumer = new PaymentConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<PaymentConfirmedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        (await context.Orders.SingleAsync(o => o.Id == order.Id)).Status.Should().Be(Bizcore.BuildingBlocks.OrderStatus.Cancelled);
    }

    [Fact]
    public async Task PaymentConfirmedConsumer_WhenOrderMissing_LogsWarning_DoesNotThrow()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var message = Mock.Of<IPaymentConfirmedEvent>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == Guid.NewGuid());

        var consumer = new PaymentConfirmedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentConfirmedConsumer>.Instance);

        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
    }
}
