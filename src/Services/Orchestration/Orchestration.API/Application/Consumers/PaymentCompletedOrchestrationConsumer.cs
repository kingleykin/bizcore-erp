using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompletedOrchestrationConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly IOrchestrationStepRecorder _recorder;

    public PaymentCompletedOrchestrationConsumer(IOrchestrationStepRecorder recorder)
    {
        _recorder = recorder;
    }

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        // ProcessFlow/FlowStep chỉ theo dõi luồng Hóa đơn — payment cho Đơn hàng không có
        // ProcessFlow tương ứng nên bỏ qua (không phải lỗi).
        if (context.Message.InvoiceId is not { } invoiceId)
            return;

        await _recorder.RecordAsync(
            invoiceId,
            InvoicePaymentFlow.Steps.PaymentCompletedObserved,
            InvoicePaymentFlow.States.PaymentCaptured,
            context.Message,
            context.Message.PaymentId,
            context.CancellationToken);
    }
}
