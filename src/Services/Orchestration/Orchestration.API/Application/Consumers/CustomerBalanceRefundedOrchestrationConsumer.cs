using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Consumers;

public class CustomerBalanceRefundedOrchestrationConsumer : IConsumer<ICustomerBalanceRefundedEvent>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<CustomerBalanceRefundedOrchestrationConsumer> _logger;

    public CustomerBalanceRefundedOrchestrationConsumer(
        AppDbContext db,
        IMediator mediator,
        ILogger<CustomerBalanceRefundedOrchestrationConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ICustomerBalanceRefundedEvent> context)
    {
        var message = context.Message;
        
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(f => f.LastPaymentId == message.PaymentId, context.CancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "[Orchestrator] ProcessFlow not found for PaymentId={PaymentId}. Cannot record CustomerBalanceRefunded step.",
                message.PaymentId);
            return;
        }

        await _mediator.Send(new RecordOrchestrationStepCommand(
            flow.InvoiceId,
            InvoicePaymentFlow.Steps.CustomerBalanceRefundedObserved,
            InvoicePaymentFlow.States.RefundingBalance,
            context.Message,
            context.Message.PaymentId
        ), context.CancellationToken);

        _logger.LogInformation(
            "[Orchestrator] Recorded CustomerBalanceRefundedObserved step for InvoiceId={InvoiceId}, PaymentId={PaymentId}",
            flow.InvoiceId, context.Message.PaymentId);
    }
}
