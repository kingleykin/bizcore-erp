using Audit.API.Domain.Enums;

namespace Audit.API.Application.DTOs;

public record AuditEntryDto(
    Guid Id,
    string? CorrelationId,
    string? TraceId,
    string? SpanId,
    string ServiceName,
    string? EntityName,
    string? EntityId,
    string Action,
    string Category,
    string Severity,
    string Outcome,
    string DataClassification,
    string? TenantId,
    string? BeforeJson,
    string? AfterJson,
    string? PerformedBy,
    string? PerformedByName,
    string? IpAddress,
    string? UserAgent,
    DateTime PerformedAt,
    string? Hash,
    string? PreviousHash
);

public record AuditQueryParams
{
    public string? ServiceName { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? PerformedBy { get; init; }
    public AuditCategory? Category { get; init; }
    public AuditSeverity? Severity { get; init; }
    public AuditOutcome? Outcome { get; init; }
    public DataClassification? DataClassification { get; init; }
    public string? TenantId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record IntegrityResult(bool IsValid, string Details, DateTime CheckedAt);

public record MarkReversedRequest(Guid ReversalEntryId, string Reason);
