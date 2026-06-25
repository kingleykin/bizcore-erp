using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Consumers;

public class CustomerPointAdditionFailedOrchestrationConsumer : IConsumer<ICustomerPointAdditionFailedEvent>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<CustomerPointAdditionFailedOrchestrationConsumer> _logger;

    public CustomerPointAdditionFailedOrchestrationConsumer(
        AppDbContext db,
        IMediator mediator,
        ILogger<CustomerPointAdditionFailedOrchestrationConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ICustomerPointAdditionFailedEvent> context)
    {
        var message = context.Message;
        
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(f => f.LastPaymentId == message.PaymentId, context.CancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "[Orchestrator] ProcessFlow not found for PaymentId={PaymentId}. Cannot record CustomerPointAdditionFailed step.",
                message.PaymentId);
            return;
        }

        await _mediator.Send(new RecordOrchestrationStepCommand(
            flow.InvoiceId,
            InvoicePaymentFlow.Steps.CustomerPointAdditionFailedObserved,
            InvoicePaymentFlow.States.Compensating,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);

        _logger.LogInformation(
            "[Orchestrator] Recorded CustomerPointAdditionFailedObserved step for InvoiceId={InvoiceId}, PaymentId={PaymentId}",
            flow.InvoiceId, context.Message.PaymentId);
    }
}
