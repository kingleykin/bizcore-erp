using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class InvoiceCreatedOrchestrationConsumer : IConsumer<IInvoiceCreatedEvent>
{
    private readonly IOrchestrationStepRecorder _recorder;

    public InvoiceCreatedOrchestrationConsumer(IOrchestrationStepRecorder recorder)
    {
        _recorder = recorder;
    }

    public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
    {
        await _recorder.RecordAsync(
            context.Message.Id,
            InvoicePaymentFlow.Steps.InvoiceCreatedObserved,
            InvoicePaymentFlow.States.InvoiceIndexed,
            context.Message,
            paymentId: null,
            context.CancellationToken);
    }
}
