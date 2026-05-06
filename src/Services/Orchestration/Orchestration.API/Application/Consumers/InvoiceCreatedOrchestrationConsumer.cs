using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Application.Services;

namespace Orchestration.API.Application.Consumers;

public class InvoiceCreatedOrchestrationConsumer : IConsumer<IInvoiceCreatedEvent>
{
    private readonly IProcessOrchestrationService _orchestration;

    public InvoiceCreatedOrchestrationConsumer(IProcessOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    public Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
        => _orchestration.RecordInvoiceCreatedAsync(context.Message, context.CancellationToken);
}
