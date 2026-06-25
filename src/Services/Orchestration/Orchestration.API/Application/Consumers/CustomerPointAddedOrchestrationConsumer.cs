using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Consumers;

public class CustomerPointAddedOrchestrationConsumer : IConsumer<ICustomerPointAddedEvent>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<CustomerPointAddedOrchestrationConsumer> _logger;

    public CustomerPointAddedOrchestrationConsumer(
        AppDbContext db,
        IMediator mediator,
        ILogger<CustomerPointAddedOrchestrationConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ICustomerPointAddedEvent> context)
    {

        var message = context.Message;
        
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(f => f.LastPaymentId == message.PaymentId, context.CancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "[Orchestrator] ProcessFlow not found for PaymentId={PaymentId}. Cannot record CustomerPointAdded step.",
                message.PaymentId);
            return;
        }

        await _mediator.Send(new RecordOrchestrationStepCommand(
            flow.InvoiceId,
            InvoicePaymentFlow.Steps.CustomerPointAddedObserved,
            InvoicePaymentFlow.States.CustomerPointAdded,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);

        _logger.LogInformation(
            "[Orchestrator] Recorded CustomerPointAddedObserved step for InvoiceId={InvoiceId}, PaymentId={PaymentId}",
            flow.InvoiceId, context.Message.PaymentId);
    }
}
