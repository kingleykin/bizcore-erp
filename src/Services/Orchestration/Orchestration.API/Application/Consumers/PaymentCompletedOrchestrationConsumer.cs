using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class PaymentCompletedOrchestrationConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly IMediator _mediator;

    public PaymentCompletedOrchestrationConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        await _mediator.Send(new RecordOrchestrationStepCommand(
            context.Message.InvoiceId,
            InvoicePaymentFlow.Steps.PaymentCompletedObserved,
            InvoicePaymentFlow.States.PaymentCaptured,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);
    }
}
