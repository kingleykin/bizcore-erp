using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;
using System.Text.Json;

namespace Orchestration.API.Application.Commands;

public record RecordOrchestrationStepCommand(
    Guid InvoiceId,
    string StepType,
    string NewState,
    object Payload,
    Guid? PaymentId = null) : IRequest, ITransactionalCommand;

public class RecordOrchestrationStepHandler : IRequestHandler<RecordOrchestrationStepCommand>
{
    private readonly AppDbContext _db;

    public RecordOrchestrationStepHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(RecordOrchestrationStepCommand request, CancellationToken ct)
    {
        var flow = await _db.ProcessFlows
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.InvoiceId == request.InvoiceId, ct);

        if (flow == null)
        {
            flow = ProcessFlow.Create(request.InvoiceId);
            flow.MoveToState(request.NewState, request.PaymentId);
            _db.ProcessFlows.Add(flow);
        }
        else
        {
            flow.MoveToState(request.NewState, request.PaymentId);
        }

        // Explicitly add the new step so EF tracks it as Added (not Modified via relationship fixup)
        var newStep = flow.AddStep(request.StepType, JsonSerializer.Serialize(request.Payload));
        _db.Set<FlowStep>().Add(newStep);
    }
}
