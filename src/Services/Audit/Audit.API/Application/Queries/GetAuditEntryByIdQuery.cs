using Audit.API.Application.DTOs;
using Audit.API.Domain.Entities;
using Audit.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Application.Queries;

public record GetAuditEntryByIdQuery(Guid Id) : IRequest<AuditEntryDto?>;

public class GetAuditEntryByIdHandler : IRequestHandler<GetAuditEntryByIdQuery, AuditEntryDto?>
{
    private readonly AuditDbContext _db;

    public GetAuditEntryByIdHandler(AuditDbContext db)
    {
        _db = db;
    }

    public async Task<AuditEntryDto?> Handle(GetAuditEntryByIdQuery request, CancellationToken ct)
    {
        var entry = await _db.AuditEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);

        return entry == null ? null : MapToDto(entry);
    }

    private static AuditEntryDto MapToDto(AuditEntry e) => new(
        e.Id, e.CorrelationId, e.TraceId, e.SpanId,
        e.ServiceName, e.EntityName, e.EntityId,
        e.Action, 
        e.Category.ToString(),
        e.Severity.ToString(),
        e.Outcome.ToString(),
        e.DataClassification.ToString(),
        e.TenantId,
        e.BeforeJson, e.AfterJson,
        e.PerformedBy, e.PerformedByName,
        e.IpAddress, e.UserAgent,
        e.PerformedAt, e.Hash, e.PreviousHash
    );
}
