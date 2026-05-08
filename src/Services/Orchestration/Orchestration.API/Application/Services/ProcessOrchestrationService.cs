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
    private readonly ILogger<ProcessOrchestrationService> _logger;

    public ProcessOrchestrationService(AppDbContext db, ILogger<ProcessOrchestrationService> logger)
    {
        _db = db;
        _logger = logger;
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
        _logger.LogInformation("Listing recent process flows with take={Take}", take);
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
        _logger.LogInformation(
            "Recording invoice creation in orchestration ProcessFlow InvoiceId={InvoiceId} CustomerName={CustomerName} Amount={Amount}",
            e.Id, e.CustomerName, e.Amount);

        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.InvoiceId == e.Id, cancellationToken);

        if (flow == null)
        {
            _logger.LogInformation(
                "No existing process flow found for InvoiceId={InvoiceId} when recording invoice creation. Creating new flow.",
                e.Id);

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
            _logger.LogInformation(
                "Updating existing process flow for InvoiceId={InvoiceId} with new invoice creation event. CurrentState={CurrentState}",
                e.Id, flow.CurrentState);

            flow.UpdatedAtUtc = now;
            flow.CurrentState = InvoicePaymentFlow.States.InvoiceIndexed;
        }

        _logger.LogInformation(
                    "Adding flow step for invoice creation ProcessFlowId={ProcessFlowId} InvoiceId={InvoiceId} CustomerName={CustomerName} Amount={Amount}",
                    flow.Id, e.Id, e.CustomerName, e.Amount);
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
        _logger.LogInformation(
            "Recording payment completion in orchestration ProcessFlow PaymentId={PaymentId} InvoiceId={InvoiceId}",
            e.PaymentId, e.InvoiceId);

        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(p => p.InvoiceId == e.InvoiceId, cancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "No existing process flow found for InvoiceId={InvoiceId} when recording payment completion. Creating new flow.",
                e.InvoiceId);

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
            _logger.LogInformation(
                "Updating existing process flow for InvoiceId={InvoiceId} with new payment completion. PaymentId={PaymentId}",
                e.InvoiceId, e.PaymentId);

            flow.LastPaymentId = e.PaymentId;
            flow.CurrentState = InvoicePaymentFlow.States.PaymentCaptured;
            flow.UpdatedAtUtc = now;
        }

        _logger.LogInformation(
            "Adding flow step for payment completion ProcessFlowId={ProcessFlowId} PaymentId={PaymentId} InvoiceId={InvoiceId}",
            flow.Id, e.PaymentId, e.InvoiceId);
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
        _logger.LogInformation(
            "Recording payment compensation request in orchestration ProcessFlow PaymentId={PaymentId} InvoiceId={InvoiceId} Reason={Reason}",
            e.PaymentId, e.InvoiceId, e.Reason);
        var now = DateTime.UtcNow;
        var flow = await _db.ProcessFlows
            .FirstOrDefaultAsync(p => p.InvoiceId == e.InvoiceId, cancellationToken);

        if (flow == null)
        {
            _logger.LogWarning(
                "No existing process flow found for InvoiceId={InvoiceId} when recording payment compensation request. Creating new flow.",
                e.InvoiceId);

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
            _logger.LogInformation(
                "Updating existing process flow for InvoiceId={InvoiceId} with new payment compensation request. PaymentId={PaymentId} Reason={Reason}",
                e.InvoiceId, e.PaymentId, e.Reason);

            flow.LastPaymentId = e.PaymentId;
            flow.CurrentState = InvoicePaymentFlow.States.CompensationRequired;
            flow.UpdatedAtUtc = now;
        }

        _logger.LogInformation(
            "Adding flow step for payment compensation request ProcessFlowId={ProcessFlowId} PaymentId={PaymentId} InvoiceId={InvoiceId} Reason={Reason}",
            flow.Id, e.PaymentId, e.InvoiceId, e.Reason);
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
