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
        await _recorder.RecordAsync(
            context.Message.InvoiceId ?? Guid.Empty,
            InvoicePaymentFlow.Steps.PaymentCompensationRequestedObserved,
            InvoicePaymentFlow.States.CompensationRequired,
            context.Message,
            context.Message.PaymentId,
            context.CancellationToken);
    }
}
