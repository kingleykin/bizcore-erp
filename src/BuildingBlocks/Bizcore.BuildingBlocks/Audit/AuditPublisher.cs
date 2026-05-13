using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;

namespace Bizcore.BuildingBlocks.Audit
{
    public class AuditPublisher : IAuditPublisher
    {
        private readonly IPublishEndpoint? _publishEndpoint;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditPublisher> _logger;
        private readonly string _serviceName;

        public AuditPublisher(
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<AuditPublisher> logger)
        {
            // IPublishEndpoint might not be registered in services that don't use messaging (e.g. Gateway)
            _publishEndpoint = serviceProvider.GetService<IPublishEndpoint>();
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _serviceName = configuration["ServiceName"] ?? "UnknownService";
        }

        public async Task PublishAsync(
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
            CancellationToken ct = default)
        {
            if (_publishEndpoint == null)
            {
                _logger.LogWarning("AuditPublisher: IPublishEndpoint is not registered. Audit event for action '{Action}' will be ignored.", action);
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var activity = Activity.Current;

            // Enrich Actor info if not provided
            actorUserId ??= user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            actorUsername ??= user?.Identity?.Name;
            tenantId ??= user?.FindFirst("tenant_id")?.Value;

            var auditEvent = new AuditEvent
            {
                // Who
                ActorUserId = actorUserId,
                ActorUsername = actorUsername,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers["User-Agent"],
                TenantId = tenantId,

                // What
                Action = action,
                Category = category,
                Severity = severity,
                Outcome = outcome,
                DataClassification = classification,
                ServiceName = _serviceName,
                EntityType = entityType,
                EntityId = entityId,
                BeforeJson = before != null ? (before is string s ? s : SensitiveFieldMasker.ToMaskedJson(before)) : null,
                AfterJson = after != null ? (after is string s2 ? s2 : SensitiveFieldMasker.ToMaskedJson(after)) : null,

                // Why / Trace
                CorrelationId = httpContext?.Items["X-Correlation-ID"]?.ToString() ?? httpContext?.Request.Headers["X-Correlation-ID"].ToString(),
                TraceId = activity?.TraceId.ToString(),
                SpanId = activity?.SpanId.ToString(),

                OccurredAt = DateTime.UtcNow
            };

            await _publishEndpoint.Publish(auditEvent, ct);
        }
    }
}
