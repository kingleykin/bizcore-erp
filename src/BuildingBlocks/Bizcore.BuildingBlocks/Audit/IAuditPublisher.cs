using Bizcore.BuildingBlocks.Contracts;

namespace Bizcore.BuildingBlocks.Audit
{
    public interface IAuditPublisher
    {
        /// <summary>
        /// Publishes an audit event with automatic context enrichment (tracing, correlation, actor info).
        /// </summary>
        Task PublishAsync(
            string action,
            string? entityType = null,
            string? entityId = null,
            object? before = null,
            object? after = null,
            string category = AuditCategory.Business,
            string severity = AuditSeverity.Info,
            string outcome = AuditOutcome.Success,
            string classification = DataClassification.Internal,
            string? actorUserId = null,
            string? actorUsername = null,
            string? tenantId = null,
            CancellationToken ct = default);
    }
}
