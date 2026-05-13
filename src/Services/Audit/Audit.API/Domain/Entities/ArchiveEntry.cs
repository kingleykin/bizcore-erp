using Audit.API.Domain.Enums;

namespace Audit.API.Domain.Entities
{
    /// <summary>
    /// Warm-tier archive for entries older than the hot retention window (180 days).
    /// Same structure as AuditEntry — preserves full forensic data.
    /// </summary>
    public class ArchiveEntry
    {
        public Guid      Id              { get; set; }
        public string?   CorrelationId   { get; set; }
        public string?   TraceId         { get; set; }
        public string?   SpanId          { get; set; }
        public string    ServiceName     { get; set; } = null!;
        public string?   EntityName      { get; set; }
        public string?   EntityId        { get; set; }
        public string    Action          { get; set; } = null!;
        public AuditCategory Category    { get; set; }
        public AuditSeverity Severity    { get; set; }
        public AuditOutcome  Outcome     { get; set; }
        public DataClassification DataClassification { get; set; }
        public string?   TenantId        { get; set; }
        public string?   BeforeJson      { get; set; }
        public string?   AfterJson       { get; set; }
        public string?   PerformedBy     { get; set; }
        public string?   PerformedByName { get; set; }
        public string?   IpAddress       { get; set; }
        public string?   UserAgent       { get; set; }
        public DateTime  PerformedAt     { get; set; }
        public string?   Hash            { get; set; }
        public string?   PreviousHash    { get; set; }

        /// <summary>Timestamp when this entry was moved to warm storage.</summary>
        public DateTime  ArchivedAt      { get; set; }

        public static ArchiveEntry FromAuditEntry(AuditEntry e) => new()
        {
            Id              = e.Id,
            CorrelationId   = e.CorrelationId,
            TraceId         = e.TraceId,
            SpanId          = e.SpanId,
            ServiceName     = e.ServiceName,
            EntityName      = e.EntityName,
            EntityId        = e.EntityId,
            Action          = e.Action,
            Category        = e.Category,
            Severity        = e.Severity,
            Outcome         = e.Outcome,
            DataClassification = e.DataClassification,
            TenantId        = e.TenantId,
            BeforeJson      = e.BeforeJson,
            AfterJson       = e.AfterJson,
            PerformedBy     = e.PerformedBy,
            PerformedByName = e.PerformedByName,
            IpAddress       = e.IpAddress,
            UserAgent       = e.UserAgent,
            PerformedAt     = e.PerformedAt,
            Hash            = e.Hash,
            PreviousHash    = e.PreviousHash,
            ArchivedAt      = DateTime.UtcNow
        };
    }
}
