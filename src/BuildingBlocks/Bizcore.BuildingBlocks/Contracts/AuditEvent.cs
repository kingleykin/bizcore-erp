namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Shared audit event contract published by all microservices to the Audit.API consumer.
    /// Publishers must mask sensitive fields BEFORE populating BeforeJson / AfterJson.
    /// </summary>
    public record AuditEvent
    {
        // ── Who ───────────────────────────────────────────────────────────────
        public string? ActorUserId     { get; init; }
        public string? ActorUsername   { get; init; }
        public string? IpAddress       { get; init; }
        public string? UserAgent       { get; init; }
        public string? TenantId        { get; init; }

        // ── What ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Standardized action name (lowercase dot notation): "invoice.created", "identity.auth.login.succeeded", etc.
        /// </summary>
        public string Action           { get; init; } = null!;

        /// <summary>security | business | financial | compliance | system</summary>
        public string Category         { get; init; } = "business";

        /// <summary>info | warning | critical</summary>
        public string Severity         { get; init; } = "info";

        /// <summary>success | failure | denied</summary>
        public string Outcome          { get; init; } = "success";

        /// <summary>public | internal | pii | financial | credential</summary>
        public string DataClassification { get; init; } = "internal";

        /// <summary>Source microservice: "Admin.API", "Invoice.API", etc.</summary>
        public string ServiceName      { get; init; } = null!;

        public string? EntityType      { get; init; }
        public string? EntityId        { get; init; }

        /// <summary>JSON snapshot BEFORE change (sensitive fields must be masked).</summary>
        public string? BeforeJson      { get; init; }

        /// <summary>JSON snapshot AFTER change (sensitive fields must be masked).</summary>
        public string? AfterJson       { get; init; }

        // ── Why / Trace ───────────────────────────────────────────────────────
        /// <summary>Business workflow ID (Saga ID, Order ID, etc.)</summary>
        public string? CorrelationId   { get; init; }

        /// <summary>OpenTelemetry TraceId from Activity.Current.TraceId.</summary>
        public string? TraceId         { get; init; }

        /// <summary>OpenTelemetry SpanId from Activity.Current.SpanId.</summary>
        public string? SpanId          { get; init; }

        // ── When ──────────────────────────────────────────────────────────────
        public DateTime OccurredAt     { get; init; } = DateTime.UtcNow;
    }
}
