using Audit.API.Application.DTOs;
using Audit.API.Domain.Entities;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Application.Services
{
    public interface IAuditQueryService
    {
        Task<PagedResult<AuditEntryDto>> QueryAsync(AuditQueryParams q, CancellationToken ct = default);
        Task<AuditEntryDto?>             GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IntegrityResult>            VerifyIntegrityAsync(CancellationToken ct = default);
    }

    public class AuditQueryService : IAuditQueryService
    {
        private readonly AuditDbContext   _db;
        private readonly HashChainService _hashChain;

        public AuditQueryService(AuditDbContext db, HashChainService hashChain)
        {
            _db        = db;
            _hashChain = hashChain;
        }

        public async Task<PagedResult<AuditEntryDto>> QueryAsync(AuditQueryParams q, CancellationToken ct = default)
        {
            var query = _db.AuditEntries.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.ServiceName))
                query = query.Where(e => e.ServiceName == q.ServiceName);

            if (!string.IsNullOrWhiteSpace(q.Action))
                query = query.Where(e => e.Action.Contains(q.Action));

            if (!string.IsNullOrWhiteSpace(q.EntityType))
                query = query.Where(e => e.EntityName == q.EntityType);

            if (!string.IsNullOrWhiteSpace(q.EntityId))
                query = query.Where(e => e.EntityId == q.EntityId);

            if (!string.IsNullOrWhiteSpace(q.PerformedBy))
                query = query.Where(e => e.PerformedBy == q.PerformedBy || e.PerformedByName == q.PerformedBy);

            if (q.AuditLevel.HasValue)
                query = query.Where(e => e.AuditLevel == q.AuditLevel.Value);

            if (q.DateFrom.HasValue)
                query = query.Where(e => e.PerformedAt >= q.DateFrom.Value);

            if (q.DateTo.HasValue)
                query = query.Where(e => e.PerformedAt <= q.DateTo.Value);

            var totalCount = await query.CountAsync(ct);
            var pageSize   = Math.Clamp(q.PageSize, 1, 200);
            var page       = Math.Max(q.Page, 1);

            var items = await query
                .OrderByDescending(e => e.PerformedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => MapToDto(e))
                .ToListAsync(ct);

            return new PagedResult<AuditEntryDto>(
                items,
                totalCount,
                page,
                pageSize,
                (int)Math.Ceiling((double)totalCount / pageSize)
            );
        }

        public async Task<AuditEntryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entry = await _db.AuditEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            return entry is null ? null : MapToDto(entry);
        }

        public async Task<IntegrityResult> VerifyIntegrityAsync(CancellationToken ct = default)
        {
            var (isValid, details) = await _hashChain.VerifyChainAsync(ct);
            return new IntegrityResult(isValid, details, DateTime.UtcNow);
        }

        private static AuditEntryDto MapToDto(AuditEntry e) => new(
            e.Id, e.CorrelationId, e.TraceId, e.SpanId,
            e.ServiceName, e.EntityName, e.EntityId,
            e.Action, e.AuditLevel.ToString(),
            e.BeforeJson, e.AfterJson,
            e.PerformedBy, e.PerformedByName,
            e.IpAddress, e.UserAgent,
            e.PerformedAt, e.Hash, e.PreviousHash
        );
    }
}
