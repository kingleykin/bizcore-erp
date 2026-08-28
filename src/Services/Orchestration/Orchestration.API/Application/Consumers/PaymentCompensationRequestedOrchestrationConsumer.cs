using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompensationRequestedOrchestrationConsumer : IConsumer<IPaymentCompensationRequestedEvent>
{
    private readonly IOrchestrationStepRecorder _recorder;

    public PaymentCompensationRequestedOrchestrationConsumer(IOrchestrationStepRecorder recorder)
    {
        _recorder = recorder;
    }

    public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
    {
        // ProcessFlow/FlowStep chỉ theo dõi luồng Hóa đơn — bồi hoàn cho Đơn hàng (OrderId, không có
        // InvoiceId, vd. do Customer.API cộng điểm thất bại vĩnh viễn) không có ProcessFlow tương
        // ứng nên bỏ qua (không phải lỗi) — khớp quy ước đã áp dụng ở
        // PaymentCompletedOrchestrationConsumer. Trước đây thiếu check này khiến compensation của
        // Order bị ghi nhầm vào Guid.Empty (không tra cứu được, không đại diện đơn nào).
        if (context.Message.InvoiceId is not { } invoiceId)
            return;

        await _recorder.RecordAsync(
            invoiceId,
            InvoicePaymentFlow.Steps.PaymentCompensationRequestedObserved,
            InvoicePaymentFlow.States.CompensationRequired,
            context.Message,
            context.Message.PaymentId,
            context.CancellationToken);
    }
}
