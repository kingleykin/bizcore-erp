using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Moq;
using Orchestration.API.Application.Consumers;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain;

namespace Bizcore.UnitTests;

// Consumer quan sát (không tham gia quyết định nghiệp vụ) bên Orchestration.API, ghi ProcessFlow
// khi có yêu cầu bồi hoàn thanh toán — chỉ áp dụng cho luồng Hóa đơn (InvoiceId), không có
// ProcessFlow tương ứng cho luồng Đơn hàng (OrderId).
public class PaymentCompensationRequestedOrchestrationConsumerTests
{
    private static Mock<ConsumeContext<IPaymentCompensationRequestedEvent>> BuildConsumeContext(
        IPaymentCompensationRequestedEvent message)
    {
        var context = new Mock<ConsumeContext<IPaymentCompensationRequestedEvent>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WhenInvoiceCompensation_RecordsProcessFlowStep()
    {
        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.InvoiceId == invoiceId && m.OrderId == (Guid?)null && m.PaymentId == paymentId);

        var recorderMock = new Mock<IOrchestrationStepRecorder>();
        var consumer = new PaymentCompensationRequestedOrchestrationConsumer(recorderMock.Object);

        await consumer.Consume(BuildConsumeContext(message).Object);

        recorderMock.Verify(r => r.RecordAsync(
            invoiceId,
            InvoicePaymentFlow.Steps.PaymentCompensationRequestedObserved,
            InvoicePaymentFlow.States.CompensationRequired,
            message,
            paymentId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenOrderCompensation_NoInvoiceId_DoesNotRecord()
    {
        // Regression: trước đây consumer ghi nhầm vào Guid.Empty khi InvoiceId null (compensation
        // của Order, vd. do Customer.API cộng điểm thất bại vĩnh viễn) — giờ phải bỏ qua hẳn, khớp
        // quy ước "Order không có ProcessFlow" đã áp dụng ở PaymentCompletedOrchestrationConsumer.
        var message = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
            m.InvoiceId == (Guid?)null && m.OrderId == Guid.NewGuid() && m.PaymentId == Guid.NewGuid());

        var recorderMock = new Mock<IOrchestrationStepRecorder>(MockBehavior.Strict);
        var consumer = new PaymentCompensationRequestedOrchestrationConsumer(recorderMock.Object);

        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        recorderMock.Verify(r => r.RecordAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
