using Audit.API.Domain.Enums;

namespace Audit.API.Domain.Entities
{
    /// <summary>
    /// Immutable audit record — append-only, never updated or deleted.
    /// Includes hash chain for tamper detection (blockchain-lite pattern).
    /// </summary>
    public class AuditEntry : Bizcore.BuildingBlocks.Abstractions.BaseEntity
    {

        // ── Distributed Tracing ───────────────────────────────────────────────
        public string? CorrelationId  { get; private set; }

        /// <summary>OpenTelemetry W3C TraceId (32-char hex).</summary>
        public string? TraceId        { get; private set; }

        /// <summary>OpenTelemetry W3C SpanId (16-char hex).</summary>
        public string? SpanId         { get; private set; }

        // ── Source ────────────────────────────────────────────────────────────
        public string  ServiceName    { get; private set; } = null!;

        // ── Subject ───────────────────────────────────────────────────────────
        public string? EntityName     { get; private set; }
        public string? EntityId       { get; private set; }

        // ── Action ────────────────────────────────────────────────────────────
        /// <summary>Structured action: "invoice.created", "identity.auth.login.succeeded", etc.</summary>
        public string  Action         { get; private set; } = null!;
        public AuditCategory Category { get; private set; }
        public AuditSeverity Severity { get; private set; }
        public AuditOutcome  Outcome  { get; private set; }
        public DataClassification DataClassification { get; private set; }
        public string? TenantId       { get; private set; }

        // ── Change Snapshot ───────────────────────────────────────────────────
        /// <summary>JSON snapshot BEFORE change (sensitive fields masked).</summary>
        public string? BeforeJson     { get; private set; }
        /// <summary>JSON snapshot AFTER change (sensitive fields masked).</summary>
        public string? AfterJson      { get; private set; }

        // ── Actor ─────────────────────────────────────────────────────────────
        public string? PerformedBy    { get; private set; }  // UserId
        public string? PerformedByName{ get; private set; }  // Username
        public string? IpAddress      { get; private set; }
        public string? UserAgent      { get; private set; }

        // ── Temporal ──────────────────────────────────────────────────────────
        public DateTime PerformedAt   { get; private set; }

        // ── Integrity (Hash Chain) ────────────────────────────────────────────
        /// <summary>SHA256 of (PreviousHash + this record's content).</summary>
        public string? Hash           { get; private set; }

        /// <summary>Hash of the preceding entry (null for the very first entry).</summary>
        public string? PreviousHash   { get; private set; }

        // ── Reversal Tracking ─────────────────────────────────────────────────
        /// <summary>True khi entry này đã được reverse bởi một DataReversal action.</summary>
        public bool    IsReversed          { get; private set; }

        /// <summary>Id của AuditEntry ghi nhận hành động Reversal (DataReversal.*).</summary>
        public Guid?   ReversedByEntryId   { get; private set; }

        /// <summary>Lý do admin cung cấp khi thực hiện reversal.</summary>
        public string? ReversalReason      { get; private set; }

        private AuditEntry() { } // EF Core

        public static AuditEntry Create(
            string serviceName,
            string action,
            AuditCategory category,
            AuditSeverity severity,
            AuditOutcome  outcome,
            DataClassification classification,
            string? tenantId       = null,
            string? correlationId  = null,
            string? traceId        = null,
            string? spanId         = null,
            string? entityName     = null,
            string? entityId       = null,
            string? beforeJson     = null,
            string? afterJson      = null,
            string? performedBy    = null,
            string? performedByName= null,
            string? ipAddress      = null,
            string? userAgent      = null,
            DateTime? occurredAt   = null,
            string? previousHash   = null)
        {
            var entry = new AuditEntry
            {
                Id              = Guid.NewGuid(),
                ServiceName     = serviceName,
                Action          = action,
                Category        = category,
                Severity        = severity,
                Outcome         = outcome,
                DataClassification = classification,
                TenantId        = tenantId,
                CorrelationId   = correlationId,
                TraceId         = traceId,
                SpanId          = spanId,
                EntityName      = entityName,
                EntityId        = entityId,
                BeforeJson      = beforeJson,
                AfterJson       = afterJson,
                PerformedBy     = performedBy,
                PerformedByName = performedByName,
                IpAddress       = ipAddress,
                UserAgent       = userAgent,
                PerformedAt     = occurredAt ?? DateTime.UtcNow,
                PreviousHash    = previousHash
            };

            return entry;
        }

        /// <summary>Assigns the computed hash and previous hash. Called by HashChainService after creation.</summary>
        public void SetHash(string? previousHash, string hash)
        {
            if (Hash is not null)
                throw new InvalidOperationException("Hash already set — AuditEntry is immutable.");
            PreviousHash = previousHash;
            Hash = hash;
        }

        /// <summary>
        /// Đánh dấu entry này đã được reverse bởi một DataReversal action.
        /// Chỉ được set một lần (idempotent guard).
        /// </summary>
        public void MarkAsReversed(Guid reversalEntryId, string reason)
        {
            if (IsReversed)
                throw new InvalidOperationException("Entry này đã được đánh dấu reversed rồi.");
            IsReversed        = true;
            ReversedByEntryId = reversalEntryId;
            ReversalReason    = reason;
        }
    }
}
