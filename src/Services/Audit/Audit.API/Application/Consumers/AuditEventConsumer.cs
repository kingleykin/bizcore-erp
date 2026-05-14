using Audit.API.Domain.Entities;
using Audit.API.Domain.Enums;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Bizcore.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace Audit.API.Application.Consumers
{
    /// <summary>
    /// Consumes AuditEvent messages from RabbitMQ.
    /// Stores each event as an immutable AuditEntry with hash chain.
    /// Retry and DLQ are configured at Program.cs level (MassTransit policy).
    /// </summary>
    public class AuditEventConsumer : IConsumer<AuditEvent>
    {
        private readonly AuditDbContext   _db;
        private readonly HashChainService _hashChain;
        private readonly ILogger<AuditEventConsumer> _logger;

        public AuditEventConsumer(
            AuditDbContext   db,
            HashChainService hashChain,
            ILogger<AuditEventConsumer> logger)
        {
            _db        = db;
            _hashChain = hashChain;
            _logger    = logger;
        }

        public async Task Consume(ConsumeContext<AuditEvent> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "AuditEvent received: [{Category}] {Service} | {Action} | Actor={Actor} | Entity={Type}/{Id}",
                msg.Category, msg.ServiceName, msg.Action,
                msg.ActorUsername ?? "system",
                msg.EntityType, msg.EntityId);

            var category = ParseEnum<AuditCategory>(msg.Category, AuditCategory.Business);
            var severity = ParseEnum<AuditSeverity>(msg.Severity, AuditSeverity.Info);
            var outcome = ParseEnum<AuditOutcome>(msg.Outcome, AuditOutcome.Success);
            var classification = ParseEnum<DataClassification>(msg.DataClassification, DataClassification.Internal);

            var entry = AuditEntry.Create(
                serviceName     : msg.ServiceName,
                action          : msg.Action,
                category        : category,
                severity        : severity,
                outcome         : outcome,
                classification  : classification,
                tenantId        : msg.TenantId,
                correlationId   : msg.CorrelationId,
                traceId         : msg.TraceId,
                spanId          : msg.SpanId,
                entityName      : msg.EntityType,
                entityId        : msg.EntityId,
                beforeJson      : msg.BeforeJson,
                afterJson       : msg.AfterJson,
                performedBy     : msg.ActorUserId,
                performedByName : msg.ActorUsername,
                ipAddress       : msg.IpAddress,
                userAgent       : msg.UserAgent,
                occurredAt      : msg.OccurredAt
            );

            await _hashChain.ComputeAndSetHashAsync(entry, context.CancellationToken);
            _db.AuditEntries.Add(entry);
            await _db.SaveChangesAsync(context.CancellationToken);

            _logger.LogDebug("AuditEntry {Id} persisted with hash {Hash}.", entry.Id, entry.Hash);
        }

        private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : defaultValue;
        }
    }
}
