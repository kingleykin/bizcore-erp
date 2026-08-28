using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Payment.API.Application.Consumers;
using Payment.API.Application.Hubs;
using Payment.API.Domain.Entities;
using PaymentEntity = Payment.API.Domain.Entities.Payment;

namespace Bizcore.UnitTests;

// Payment.Status = Fulfilled là trạng thái CUỐI của luồng thanh toán Đơn hàng — chỉ đạt được khi cả
// chuỗi phía sau (Order.Confirm, Inventory.Commit, cộng điểm) đã xong. Đây là tín hiệu duy nhất mà
// client được phép hiểu là "thanh toán thành công"; Completed thì chưa, vì còn có thể bị bồi hoàn.
public class OrderPaymentFulfilledConsumerTests
{
    private static Mock<ConsumeContext<IOrderPaymentFulfilledEvent>> BuildConsumeContext(IOrderPaymentFulfilledEvent message)
    {
        var context = new Mock<ConsumeContext<IOrderPaymentFulfilledEvent>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static (Mock<IHubContext<PaymentHub>> Hub, Mock<IClientProxy> Proxy) BuildHub(Guid paymentId)
    {
        var hubContextMock = new Mock<IHubContext<PaymentHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(paymentId.ToString())).Returns(clientProxyMock.Object);
        return (hubContextMock, clientProxyMock);
    }

    [Fact]
    public async Task Consume_WhenPaymentCompleted_MarksFulfilled_AndPushesFinalSuccessToClient()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var orderId = Guid.NewGuid();
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = 1_500_000m,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Completed
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var (hub, proxy) = BuildHub(payment.Id);
        var message = Mock.Of<IOrderPaymentFulfilledEvent>(m => m.PaymentId == payment.Id && m.OrderId == orderId);

        var consumer = new OrderPaymentFulfilledConsumer(context, hub.Object, NullLogger<OrderPaymentFulfilledConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Payments.Single(p => p.Id == payment.Id).Status.Should().Be(PaymentStatus.Fulfilled);
        proxy.Verify(p => p.SendCoreAsync(
            "PaymentStatusUpdated", It.Is<object[]>(a => a.Length == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenPaymentAlreadyReversed_DoesNotOverwriteWithFulfilled()
    {
        // Bồi hoàn luôn là kết quả cuối cùng: nếu một nhánh song song (vd. Inventory.Commit lỗi) đã
        // đảo ngược payment, tin "fulfilled" tới sau KHÔNG được phép ghi đè ngược thành thành công.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Amount = 500_000m,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Reversed
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var (hub, proxy) = BuildHub(payment.Id);
        var message = Mock.Of<IOrderPaymentFulfilledEvent>(m => m.PaymentId == payment.Id && m.OrderId == payment.OrderId!.Value);

        var consumer = new OrderPaymentFulfilledConsumer(context, hub.Object, NullLogger<OrderPaymentFulfilledConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Payments.Single(p => p.Id == payment.Id).Status.Should().Be(PaymentStatus.Reversed);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_WhenPaymentMissing_DoesNotThrow()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreatePaymentDbContext(connection);

        var (hub, _) = BuildHub(Guid.NewGuid());
        var message = Mock.Of<IOrderPaymentFulfilledEvent>(m => m.PaymentId == Guid.NewGuid() && m.OrderId == Guid.NewGuid());

        var consumer = new OrderPaymentFulfilledConsumer(context, hub.Object, NullLogger<OrderPaymentFulfilledConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
    }
}
