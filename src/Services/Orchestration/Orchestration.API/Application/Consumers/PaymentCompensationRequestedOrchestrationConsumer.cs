using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompensationRequestedOrchestrationConsumer : IConsumer<IPaymentCompensationRequestedEvent>
{
    private readonly IProcessOrchestrationService _orchestration;

    public PaymentCompensationRequestedOrchestrationConsumer(IProcessOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    public Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
        => _orchestration.RecordCompensationRequestedAsync(context.Message, context.CancellationToken);
}
