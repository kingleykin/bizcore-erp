using Audit.API.Domain.Entities;
using Audit.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Application.Jobs
{
    /// <summary>
    /// Hangfire daily job: moves AuditEntries older than the hot retention window
    /// to ArchiveEntries (warm storage). Deletes moved records from hot storage.
    ///
    /// Retention tiers:
    ///   Hot  (AuditEntries)  → last 180 days  (operational investigation)
    ///   Warm (ArchiveEntries) → beyond 180 days (compliance, kept indefinitely or 5 years)
    /// </summary>
    public class RetentionCleanupJob
    {
        private readonly AuditDbContext _db;
        private readonly ILogger<RetentionCleanupJob> _logger;
        private readonly IConfiguration _config;

        public RetentionCleanupJob(
            AuditDbContext db,
            ILogger<RetentionCleanupJob> logger,
            IConfiguration config)
        {
            _db     = db;
            _logger = logger;
            _config = config;
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            var hotDays  = _config.GetValue<int>("Retention:HotDays", 180);
            var cutoff   = DateTime.UtcNow.AddDays(-hotDays);
            var batchSize = 500;
            var totalMoved = 0;

            _logger.LogInformation("RetentionCleanupJob started. Cutoff: {Cutoff} ({HotDays} days)", cutoff, hotDays);

            while (true)
            {
                var batch = await _db.AuditEntries
                    .Where(e => e.PerformedAt < cutoff)
                    .OrderBy(e => e.PerformedAt)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                var archives = batch.Select(ArchiveEntry.FromAuditEntry).ToList();

                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    _db.ArchiveEntries.AddRange(archives);
                    _db.AuditEntries.RemoveRange(batch);
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }

                totalMoved += batch.Count;
                _logger.LogDebug("Archived batch of {Count}. Total so far: {Total}", batch.Count, totalMoved);

                if (batch.Count < batchSize) break;
            }

            _logger.LogInformation("RetentionCleanupJob completed. Total moved to archive: {Total}", totalMoved);
        }
    }
}
