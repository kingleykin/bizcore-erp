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

        // ── What ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Structured action name: "Invoice.Created", "Auth.Login.Failed", "Payment.Reversed", etc.
        /// </summary>
        public string Action           { get; init; } = null!;

        /// <summary>Security | Financial | Operational | Compliance</summary>
        public string AuditLevel       { get; init; } = "Operational";

        /// <summary>Source microservice: "Invoice.API", "Payment.API", etc.</summary>
        public string ServiceName      { get; init; } = null!;

        public string? EntityType      { get; init; }
        public string? EntityId        { get; init; }

        /// <summary>JSON snapshot BEFORE change (sensitive fields must be masked).</summary>
        public string? BeforeJson      { get; init; }

        /// <summary>JSON snapshot AFTER change (sensitive fields must be masked).</summary>
        public string? AfterJson       { get; init; }

        // ── Why / Trace ───────────────────────────────────────────────────────
        public string? CorrelationId   { get; init; }

        /// <summary>OpenTelemetry TraceId from Activity.Current.TraceId.</summary>
        public string? TraceId         { get; init; }

        /// <summary>OpenTelemetry SpanId from Activity.Current.SpanId.</summary>
        public string? SpanId          { get; init; }

        // ── When ──────────────────────────────────────────────────────────────
        public DateTime OccurredAt     { get; init; } = DateTime.UtcNow;
    }
}
