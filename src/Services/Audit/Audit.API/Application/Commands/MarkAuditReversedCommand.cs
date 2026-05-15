using Audit.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Application.Commands;

public record MarkAuditReversedCommand(Guid Id, Guid ReversalEntryId, string Reason) : IRequest, ITransactionalCommand;

public class MarkAuditReversedHandler : IRequestHandler<MarkAuditReversedCommand>
{
    private readonly AuditDbContext _db;
    private readonly ILogger<MarkAuditReversedHandler> _logger;

    public MarkAuditReversedHandler(AuditDbContext db, ILogger<MarkAuditReversedHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(MarkAuditReversedCommand command, CancellationToken ct)
    {
        var entry = await _db.AuditEntries.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (entry == null)
        {
            _logger.LogWarning("Audit entry {Id} not found for marking as reversed.", command.Id);
            return;
        }

        entry.MarkAsReversed(command.ReversalEntryId, command.Reason);
        // SaveChangesAsync is intentionally omitted here.
        // TransactionBehavior.CommitAsync (via ITransactionalCommand) handles persistence.
        
        _logger.LogInformation("Audit entry {Id} marked as reversed by {ReversalEntryId}.", command.Id, command.ReversalEntryId);
    }
}
