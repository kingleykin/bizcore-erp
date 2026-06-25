using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class PaymentInitiatedOrchestrationConsumer : IConsumer<IPaymentInitiatedEvent>
{
    private readonly IMediator _mediator;

    public PaymentInitiatedOrchestrationConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<IPaymentInitiatedEvent> context)
    {
        await _mediator.Send(new RecordOrchestrationStepCommand(
            context.Message.InvoiceId,
            InvoicePaymentFlow.Steps.PaymentInitiatedObserved,
            InvoicePaymentFlow.States.PaymentInitiated,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);
    }
}
