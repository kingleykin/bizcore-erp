using Audit.API.Domain.Enums;

namespace Audit.API.Application.DTOs
{
    public record AuditEntryDto(
        Guid      Id,
        string?   CorrelationId,
        string?   TraceId,
        string?   SpanId,
        string    ServiceName,
        string?   EntityName,
        string?   EntityId,
        string    Action,
        string    AuditLevel,
        string?   BeforeJson,
        string?   AfterJson,
        string?   PerformedBy,
        string?   PerformedByName,
        string?   IpAddress,
        string?   UserAgent,
        DateTime  PerformedAt,
        string?   Hash,
        string?   PreviousHash
    );

    public record AuditQueryParams
    {
        public string?    ServiceName  { get; init; }
        public string?    Action       { get; init; }
        public string?    EntityType   { get; init; }
        public string?    EntityId     { get; init; }
        public string?    PerformedBy  { get; init; }
        public AuditLevel? AuditLevel  { get; init; }
        public DateTime?  DateFrom     { get; init; }
        public DateTime?  DateTo       { get; init; }
        public int        Page         { get; init; } = 1;
        public int        PageSize     { get; init; } = 50;
    }

    public record PagedResult<T>(
        IEnumerable<T> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public record IntegrityResult(bool IsValid, string Details, DateTime CheckedAt);
}
