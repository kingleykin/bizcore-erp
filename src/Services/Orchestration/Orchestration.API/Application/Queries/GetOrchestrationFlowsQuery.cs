using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Application.DTOs;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Queries;

public record GetOrchestrationFlowsQuery(int Take = 50) : IRequest<IReadOnlyList<ProcessFlowDto>>;

public class GetOrchestrationFlowsHandler : IRequestHandler<GetOrchestrationFlowsQuery, IReadOnlyList<ProcessFlowDto>>
{
    private readonly AppDbContext _db;

    public GetOrchestrationFlowsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProcessFlowDto>> Handle(GetOrchestrationFlowsQuery request, CancellationToken ct)
    {
        var flows = await _db.ProcessFlows
            .Include(f => f.Steps)
            .OrderByDescending(f => f.UpdatedAt)
            .Take(request.Take)
            .AsNoTracking()
            .ToListAsync(ct);

        return flows.Select(flow => new ProcessFlowDto(
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
                .ToList())).ToList();
    }
}
