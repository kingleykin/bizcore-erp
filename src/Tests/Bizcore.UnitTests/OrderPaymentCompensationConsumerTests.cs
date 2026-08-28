using Bizcore.BuildingBlocks;
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

// Consumer nhận IPaymentCompensationRequestedEvent khi 1 bước SAU khi Order đã Confirm (vd. cộng
// điểm khách hàng ở Customer.API) thất bại vĩnh viễn — đảo ngược Order.Status từ Confirmed về ĐÚNG
// trạng thái trước khi xử lý thanh toán (Pending, không phải Cancelled — Order.Revert() theo đúng
// chuẩn compensating transaction), khớp với việc Payment.API cùng lúc chuyển Payment.Status =
// Reversed. Đơn sau khi revert có thể được thanh toán lại bình thường.
public class OrderPaymentCompensationConsumerTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WhenOrderConfirmed_RevertsToPending_AndPublishesOrderRevertedEventForInventory()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách A", null, [(productId, "SP", 2, 800_000m)]);
        order.Confirm();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Reason == "Cộng điểm thất bại sau nhiều lần thử lại");

        OrderRevertedEvent? published = null;
        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish(It.IsAny<OrderRevertedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderRevertedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var consumer = new PaymentCompensationRequestedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var updated = await context.Orders.SingleAsync(o => o.Id == order.Id);
        updated.Status.Should().Be(OrderStatus.Pending,
            "bồi hoàn phải trả đơn về đúng trạng thái trước khi thanh toán, không phải Cancelled");
        updated.CancelReason.Should().BeNull("Revert() không phải hành động hủy nên không ghi CancelReason");

        published.Should().NotBeNull("phải báo cho Inventory Service nhập lại kho đã Commit");
        published!.Id.Should().Be(order.Id);
        published.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 2);
    }

    [Fact]
    public async Task Consume_WhenOrderIdIsNull_DoesNothing()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == (Guid?)null && m.InvoiceId == Guid.NewGuid());

        var consumer = new PaymentCompensationRequestedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);

        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        context.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_WhenOrderAlreadyCancelled_DoesNotThrow_DoesNotChangeStatus_DoesNotPublish()
    {
        // Race hiếm: đơn đã bị hủy (bởi lý do khác, hoặc đã được revert từ trước — message bị
        // redeliver) — Revert() chỉ cho phép từ Confirmed nên throw, consumer bắt lại, không được
        // "hồi sinh" đơn đã Cancelled về Pending, và không được báo Inventory nhập lại kho sai.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách B", null, [(Guid.NewGuid(), "SP", 1, 100_000m)]);
        order.Cancel("khách hủy trước đó");
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Reason == "lý do khác");

        var consumer = new PaymentCompensationRequestedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        var unchanged = await context.Orders.SingleAsync(o => o.Id == order.Id);
        unchanged.Status.Should().Be(OrderStatus.Cancelled);
        unchanged.CancelReason.Should().Be("khách hủy trước đó", "không được ghi đè lý do hủy đã có từ trước");
    }

    [Fact]
    public async Task Consume_WhenOrderStillPending_LogsError_DoesNotThrow_DoesNotChangeStatus()
    {
        // Race hiếm: đơn chưa từng Confirm (vẫn Pending) nhưng lại nhận yêu cầu bồi hoàn — Revert()
        // chỉ cho phép từ Confirmed nên throw DomainException, consumer phải bắt lại để không retry
        // vô ích (không phải lỗi thoáng qua).
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách C", null, [(Guid.NewGuid(), "SP", 1, 100_000m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == order.Id && m.Reason == "lý do");

        var consumer = new PaymentCompensationRequestedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        (await context.Orders.SingleAsync(o => o.Id == order.Id)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Consume_WhenOrderMissing_LogsWarning_DoesNotThrow()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.PaymentId == Guid.NewGuid() && m.OrderId == Guid.NewGuid() && m.Reason == "lý do");

        var consumer = new PaymentCompensationRequestedConsumer(
            context, Mock.Of<IPublishEndpoint>(MockBehavior.Strict), Mock.Of<IAuditPublisher>(), NullLogger<PaymentCompensationRequestedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
    }
}
