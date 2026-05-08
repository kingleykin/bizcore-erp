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
                "AuditEvent received: [{Level}] {Service} | {Action} | Actor={Actor} | Entity={Type}/{Id}",
                msg.AuditLevel, msg.ServiceName, msg.Action,
                msg.ActorUsername ?? "system",
                msg.EntityType, msg.EntityId);

            var level = ParseLevel(msg.AuditLevel);

            var entry = AuditEntry.Create(
                serviceName     : msg.ServiceName,
                action          : msg.Action,
                auditLevel      : level,
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

            // Wrap hash chain computation and insert in a transaction.
            // Using ReadCommitted with explicit row-level locking in HashChainService
            // provides better concurrency than global Serializable.
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, context.CancellationToken);

            try
            {
                // Compute hash chain INSIDE the same DbContext scope and transaction
                await _hashChain.ComputeAndSetHashAsync(entry, context.CancellationToken);

                _db.AuditEntries.Add(entry);
                await _db.SaveChangesAsync(context.CancellationToken);

                await transaction.CommitAsync(context.CancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(context.CancellationToken);
                throw;
            }

            _logger.LogDebug("AuditEntry {Id} persisted with hash {Hash}.", entry.Id, entry.Hash);
        }

        private static AuditLevel ParseLevel(string? level) =>
            Enum.TryParse<AuditLevel>(level, ignoreCase: true, out var result)
                ? result
                : AuditLevel.Operational;
    }
}
