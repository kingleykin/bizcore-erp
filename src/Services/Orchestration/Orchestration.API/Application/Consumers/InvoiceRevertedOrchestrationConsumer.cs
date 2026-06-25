using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Consumers;

public class InvoiceRevertedOrchestrationConsumer : IConsumer<IInvoicePaymentRevertedEvent>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<InvoiceRevertedOrchestrationConsumer> _logger;

    public InvoiceRevertedOrchestrationConsumer(
        AppDbContext db,
        IMediator mediator,
        ILogger<InvoiceRevertedOrchestrationConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IInvoicePaymentRevertedEvent> context)
    {
        var message = context.Message;
        
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(f => f.InvoiceId == message.InvoiceId, context.CancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "[Orchestrator] ProcessFlow not found for InvoiceId={InvoiceId}. Cannot record InvoiceReverted step.",
                message.InvoiceId);
            return;
        }

        await _mediator.Send(new RecordOrchestrationStepCommand(
            flow.InvoiceId,
            InvoicePaymentFlow.Steps.InvoicePaymentRevertedObserved,
            InvoicePaymentFlow.States.Failed,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);

        _logger.LogInformation(
            "[Orchestrator] Recorded InvoicePaymentRevertedObserved step for InvoiceId={InvoiceId}",
            flow.InvoiceId);
    }
}
