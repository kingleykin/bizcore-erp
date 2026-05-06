using System.Text.Json;
using Bizcore.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Domain;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Services;

public class ProcessOrchestrationService : IProcessOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AppDbContext _db;

    public ProcessOrchestrationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProcessFlow?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var flow = await _db.ProcessFlows
            .AsNoTracking()
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId, cancellationToken);

        if (flow == null) return null;
        flow.Steps = flow.Steps.OrderBy(s => s.OccurredAtUtc).ToList();
        return flow;
    }

    public async Task<IReadOnlyList<ProcessFlow>> ListRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var flows = await _db.ProcessFlows
            .AsNoTracking()
            .Include(p => p.Steps)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        foreach (var flow in flows)
        {
            flow.Steps = flow.Steps.OrderBy(s => s.OccurredAtUtc).ToList();
        }

        return flows;
    }

    public async Task RecordInvoiceCreatedAsync(IInvoiceCreatedEvent e, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.InvoiceId == e.Id, cancellationToken);

        if (flow == null)
        {
            flow = new ProcessFlow
            {
                Id = Guid.NewGuid(),
                InvoiceId = e.Id,
                FlowType = InvoicePaymentFlow.FlowTypeConstant,
                CurrentState = InvoicePaymentFlow.States.InvoiceIndexed,
                StartedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.ProcessFlows.Add(flow);
        }
        else
        {
            flow.UpdatedAtUtc = now;
            flow.CurrentState = InvoicePaymentFlow.States.InvoiceIndexed;
        }

        _db.FlowSteps.Add(new FlowStep
        {
            Id = Guid.NewGuid(),
            ProcessFlowId = flow.Id,
            StepType = InvoicePaymentFlow.Steps.InvoiceCreatedObserved,
            PayloadJson = JsonSerializer.Serialize(new
            {
                e.Id,
                e.CustomerName,
                e.Amount,
                e.CreatedAt
            }, JsonOptions),
            OccurredAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordPaymentCompletedAsync(IPaymentCompletedEvent e, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(p => p.InvoiceId == e.InvoiceId, cancellationToken);

        if (flow == null)
        {
            flow = new ProcessFlow
            {
                Id = Guid.NewGuid(),
                InvoiceId = e.InvoiceId,
                FlowType = InvoicePaymentFlow.FlowTypeConstant,
                CurrentState = InvoicePaymentFlow.States.PaymentCaptured,
                StartedAtUtc = now,
                UpdatedAtUtc = now,
                LastPaymentId = e.PaymentId
            };
            _db.ProcessFlows.Add(flow);
        }
        else
        {
            flow.LastPaymentId = e.PaymentId;
            flow.CurrentState = InvoicePaymentFlow.States.PaymentCaptured;
            flow.UpdatedAtUtc = now;
        }

        _db.FlowSteps.Add(new FlowStep
        {
            Id = Guid.NewGuid(),
            ProcessFlowId = flow.Id,
            StepType = InvoicePaymentFlow.Steps.PaymentCompletedObserved,
            PayloadJson = JsonSerializer.Serialize(new
            {
                e.PaymentId,
                e.InvoiceId,
                e.Amount,
                e.PaymentDate
            }, JsonOptions),
            OccurredAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCompensationRequestedAsync(IPaymentCompensationRequestedEvent e, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(p => p.InvoiceId == e.InvoiceId, cancellationToken);

        if (flow == null)
        {
            flow = new ProcessFlow
            {
                Id = Guid.NewGuid(),
                InvoiceId = e.InvoiceId,
                FlowType = InvoicePaymentFlow.FlowTypeConstant,
                CurrentState = InvoicePaymentFlow.States.CompensationRequired,
                StartedAtUtc = now,
                UpdatedAtUtc = now,
                LastPaymentId = e.PaymentId
            };
            _db.ProcessFlows.Add(flow);
        }
        else
        {
            flow.LastPaymentId = e.PaymentId;
            flow.CurrentState = InvoicePaymentFlow.States.CompensationRequired;
            flow.UpdatedAtUtc = now;
        }

        _db.FlowSteps.Add(new FlowStep
        {
            Id = Guid.NewGuid(),
            ProcessFlowId = flow.Id,
            StepType = InvoicePaymentFlow.Steps.PaymentCompensationRequestedObserved,
            PayloadJson = JsonSerializer.Serialize(new
            {
                e.PaymentId,
                e.InvoiceId,
                e.Amount,
                e.RequestedAt,
                e.Reason
            }, JsonOptions),
            OccurredAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
