using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompensationRequestedOrchestrationConsumer : IConsumer<IPaymentCompensationRequestedEvent>
{
    private readonly IMediator _mediator;

    public PaymentCompensationRequestedOrchestrationConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
    {
        await _mediator.Send(new RecordOrchestrationStepCommand(
            context.Message.InvoiceId,
            InvoicePaymentFlow.Steps.PaymentCompensationRequestedObserved,
            InvoicePaymentFlow.States.CompensationRequired,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);
    }
}
