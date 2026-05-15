using Bizcore.BuildingBlocks.Reversal;
using Invoice.API.Application.Clients;
using Invoice.API.Application.Policies;
using Invoice.API.Infrastructure.Data;
using MediatR;
using System.Security.Claims;

namespace Invoice.API.Application.Queries;

public record GetInvoiceRestoreSuggestionQuery(
    Guid InvoiceId,
    Guid AuditEntryId,
    ClaimsPrincipal Actor
) : IRequest<RestoreSuggestion?>;

public class GetInvoiceRestoreSuggestionHandler : IRequestHandler<GetInvoiceRestoreSuggestionQuery, RestoreSuggestion?>
{
    private readonly AppDbContext _context;
    private readonly IAuditServiceClient _auditClient;

    public GetInvoiceRestoreSuggestionHandler(AppDbContext context, IAuditServiceClient auditClient)
    {
        _context = context;
        _auditClient = auditClient;
    }

    public async Task<RestoreSuggestion?> Handle(GetInvoiceRestoreSuggestionQuery request, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, ct);
        if (invoice is null) return null;

        var auditEntry = await _auditClient.GetEntryAsync(request.AuditEntryId, ct);
        if (auditEntry?.BeforeJson is null) return null;

        return RestoreDiffEngine.ComputeDiff(
            auditEntryId: request.AuditEntryId,
            entityType: "Invoice",
            entityId: request.InvoiceId.ToString(),
            changedAt: auditEntry.PerformedAt,
            originalAction: auditEntry.Action,
            beforeJson: auditEntry.BeforeJson,
            currentEntity: invoice,
            policy: new InvoiceReversalPolicy(),
            actor: request.Actor);
    }
}
