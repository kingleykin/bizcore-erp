using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompletedOrchestrationConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly IProcessOrchestrationService _orchestration;

    public PaymentCompletedOrchestrationConsumer(IProcessOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    public Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
        => _orchestration.RecordPaymentCompletedAsync(context.Message, context.CancellationToken);
}
