using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;

namespace Orchestration.API.Application.Consumers;

public class InvoiceCreatedOrchestrationConsumer : IConsumer<IInvoiceCreatedEvent>
{
    private readonly IMediator _mediator;

    public InvoiceCreatedOrchestrationConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
    {
        await _mediator.Send(new RecordOrchestrationStepCommand(
            context.Message.Id,
            InvoicePaymentFlow.Steps.InvoiceCreatedObserved,
            InvoicePaymentFlow.States.InvoiceIndexed,
            context.Message
        ), context.CancellationToken);
    }
}
