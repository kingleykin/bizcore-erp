using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Application.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcore.UnitTests;

// Consumer nhận Fault<OrderConfirmedEvent> — chỉ được MassTransit publish sau khi
// OrderConfirmedConsumer (cộng điểm) throw ở TẤT CẢ các lần retry (lỗi vĩnh viễn, không phải thoáng
// qua). Đây là nơi DUY NHẤT yêu cầu bồi hoàn thanh toán cho lỗi cộng điểm.
public class OrderConfirmedFaultConsumerTests
{
    private static Mock<ConsumeContext<Fault<OrderConfirmedEvent>>> BuildFaultContext(Fault<OrderConfirmedEvent> fault)
    {
        var context = new Mock<ConsumeContext<Fault<OrderConfirmedEvent>>>();
        context.SetupGet(c => c.Message).Returns(fault);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WhenPointsAwardPermanentlyFails_RequestsPaymentCompensation()
    {
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var originalMessage = new OrderConfirmedEvent(
            orderId, Guid.NewGuid(), "Khách A", 1_500_000m, [], DateTime.UtcNow, PaymentId: paymentId);

        var fault = Mock.Of<Fault<OrderConfirmedEvent>>(f => f.Message == originalMessage);

        object? published = null;
        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = values)
            .Returns(Task.CompletedTask);

        var consumer = new OrderConfirmedFaultConsumer(publishMock.Object, NullLogger<OrderConfirmedFaultConsumer>.Instance);
        await consumer.Consume(BuildFaultContext(fault).Object);

        published.Should().NotBeNull();
        var type = published!.GetType();
        ((Guid)type.GetProperty("PaymentId")!.GetValue(published)!).Should().Be(paymentId);
        ((Guid?)type.GetProperty("OrderId")!.GetValue(published)!).Should().Be(orderId);
        ((decimal)type.GetProperty("Amount")!.GetValue(published)!).Should().Be(1_500_000m);
        ((string)type.GetProperty("Reason")!.GetValue(published)!).Should().Contain("Cộng điểm khách hàng thất bại");
    }

    [Fact]
    public async Task Consume_WhenOriginalMessageHasNoPaymentId_DoesNotPublish()
    {
        // Không nên xảy ra thực tế (OrderConfirmedConsumer return sớm khi PaymentId null nên không
        // thể fault), nhưng vẫn test để đảm bảo consumer không crash / không compensate sai.
        var originalMessage = new OrderConfirmedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Khách B", 500_000m, [], DateTime.UtcNow, PaymentId: null);

        var fault = Mock.Of<Fault<OrderConfirmedEvent>>(f => f.Message == originalMessage);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);

        var consumer = new OrderConfirmedFaultConsumer(publishMock.Object, NullLogger<OrderConfirmedFaultConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildFaultContext(fault).Object);

        await act.Should().NotThrowAsync();
    }
}
