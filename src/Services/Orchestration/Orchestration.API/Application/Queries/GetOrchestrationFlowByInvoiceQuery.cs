using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Application.DTOs;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Queries;

public record GetOrchestrationFlowByInvoiceQuery(Guid InvoiceId) : IRequest<ProcessFlowDto?>;

public class GetOrchestrationFlowByInvoiceHandler : IRequestHandler<GetOrchestrationFlowByInvoiceQuery, ProcessFlowDto?>
{
    private readonly AppDbContext _db;

    public GetOrchestrationFlowByInvoiceHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProcessFlowDto?> Handle(GetOrchestrationFlowByInvoiceQuery request, CancellationToken ct)
    {
        var flow = await _db.ProcessFlows
            .Include(f => f.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.InvoiceId == request.InvoiceId, ct);

        if (flow == null) return null;

        return new ProcessFlowDto(
            flow.Id,
            flow.InvoiceId,
            flow.FlowType,
            flow.CurrentState,
            flow.LastPaymentId,
            flow.CreatedAt,
            flow.UpdatedAt,
            flow.Steps
                .OrderBy(s => s.CreatedAt)
                .Select(s => new FlowStepDto(s.Id, s.StepType, s.PayloadJson, s.CreatedAt))
                .ToList());
    }
}
