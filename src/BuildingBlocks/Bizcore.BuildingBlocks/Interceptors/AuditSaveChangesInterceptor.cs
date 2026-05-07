using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;

namespace Bizcore.BuildingBlocks.Interceptors
{
    /// <summary>
    /// EF Core SaveChanges interceptor — automatically captures field-level Before/After
    /// for any entity implementing <see cref="IAuditable"/>.
    ///
    /// Triggers on: Add, Modify, Delete.
    /// Action name: "FieldChange.{EntityType}.{State}"
    /// AuditLevel: "Operational" (override in Application Layer for Financial/Security events).
    ///
    /// Requires IBus (MassTransit) registered in DI.
    /// Requires IHttpContextAccessor for actor/IP extraction.
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IBus? _bus;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly string _serviceName;

        // Snapshot captured BEFORE SaveChanges (to populate BeforeJson)
        private List<AuditSnapshot> _snapshots = new();

        public AuditSaveChangesInterceptor(
            string serviceName,
            IBus? bus = null,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _serviceName         = serviceName;
            _bus                 = bus;
            _httpContextAccessor = httpContextAccessor;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken ct = default)
        {
            // Capture state BEFORE save
            _snapshots = CaptureSnapshots(eventData.Context);
            return base.SavingChangesAsync(eventData, result, ct);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken ct = default)
        {
            // After save succeeds — publish field-level audit events
            if (_bus is not null && _snapshots.Count > 0)
            {
                var actor    = GetActorUserId();
                var actorName= GetActorUsername();
                var ip       = GetIpAddress();
                var userAgent= GetUserAgent();
                var correlationId = GetCorrelationId();
                var (traceId, spanId) = GetTraceContext();

                foreach (var snap in _snapshots)
                {
                    var auditEvent = new AuditEvent
                    {
                        ServiceName   = _serviceName,
                        Action        = $"FieldChange.{snap.EntityType}.{snap.State}",
                        AuditLevel    = "Operational",
                        EntityType    = snap.EntityType,
                        EntityId      = snap.EntityId,
                        BeforeJson    = snap.BeforeJson,
                        AfterJson     = snap.AfterJson,
                        ActorUserId   = actor,
                        ActorUsername = actorName,
                        IpAddress     = ip,
                        UserAgent     = userAgent,
                        CorrelationId = correlationId,
                        TraceId       = traceId,
                        SpanId        = spanId,
                        OccurredAt    = DateTime.UtcNow
                    };

                    await _bus.Publish(auditEvent, ct);
                }

                _snapshots.Clear();
            }

            return await base.SavedChangesAsync(eventData, result, ct);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<AuditSnapshot> CaptureSnapshots(DbContext? context)
        {
            if (context is null) return new();

            var snapshots = new List<AuditSnapshot>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is not IAuditable) continue;
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

                var entityType = entry.Entity.GetType().Name;
                var entityId   = entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                    ?.CurrentValue?.ToString();

                string? beforeJson = null;
                string? afterJson  = null;

                if (entry.State == EntityState.Modified)
                {
                    var before = entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                    var after  = entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

                    beforeJson = SensitiveFieldMasker.ToMaskedJson(before);
                    afterJson  = SensitiveFieldMasker.ToMaskedJson(after);
                }
                else if (entry.State == EntityState.Added)
                {
                    var values = entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                    afterJson = SensitiveFieldMasker.ToMaskedJson(values);
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var values = entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                    beforeJson = SensitiveFieldMasker.ToMaskedJson(values);
                }

                snapshots.Add(new AuditSnapshot(entityType, entityId, entry.State.ToString(), beforeJson, afterJson));
            }

            return snapshots;
        }

        private string? GetActorUserId() =>
            _httpContextAccessor?.HttpContext?.User
                .FindFirst("sub")?.Value ??
            _httpContextAccessor?.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        private string? GetActorUsername() =>
            _httpContextAccessor?.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ??
            _httpContextAccessor?.HttpContext?.User
                .FindFirst("unique_name")?.Value;

        private string? GetIpAddress() =>
            _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() =>
            _httpContextAccessor?.HttpContext?.Request.Headers["User-Agent"].ToString();

        private string? GetCorrelationId() =>
            _httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString() ??
            _httpContextAccessor?.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();

        private static (string? traceId, string? spanId) GetTraceContext()
        {
            var activity = Activity.Current;
            if (activity is null) return (null, null);
            return (activity.TraceId.ToString(), activity.SpanId.ToString());
        }

        private record AuditSnapshot(
            string EntityType,
            string? EntityId,
            string State,
            string? BeforeJson,
            string? AfterJson);
    }
}
