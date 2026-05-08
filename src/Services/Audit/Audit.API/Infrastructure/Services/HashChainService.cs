using Audit.API.Domain.Entities;
using Audit.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Audit.API.Infrastructure.Services
{
    /// <summary>
    /// Computes and verifies the SHA-256 hash chain on audit entries.
    /// Chain: Hash(n) = SHA256( PreviousHash(n) + content(n) )
    /// Any tampering with a record breaks the chain from that point onward.
    /// </summary>
    public class HashChainService
    {
        private readonly AuditDbContext _db;

        public HashChainService(AuditDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Fetches the most recent hash from the DB (via AuditHashChainHeads) and computes the hash
        /// for the provided entry. Mutates entry.Hash, entry.PreviousHash, and updates/inserts AuditHashChainHead.
        /// Must be called INSIDE the same transaction as SaveChanges.
        /// </summary>
        public async Task ComputeAndSetHashAsync(AuditEntry entry, CancellationToken ct = default)
        {
            var partitionKey = string.IsNullOrEmpty(entry.EntityName) ? "Global" : entry.EntityName;

            // Fetch head with UPDLOCK and ROWLOCK to ensure serialized append for this partition.
            // This allows us to use ReadCommitted isolation level safely.
            var head = await _db.AuditHashChainHeads
                .FromSqlRaw("SELECT * FROM AuditHashChainHeads WITH (UPDLOCK, ROWLOCK) WHERE PartitionKey = {0}", partitionKey)
                .FirstOrDefaultAsync(ct);

            string? previousHash = head?.CurrentHash;
            long nextSequence = (head?.Sequence ?? 0) + 1;

            var content = BuildContent(entry, previousHash);
            var hash = ComputeSha256(content);
            
            entry.SetHash(previousHash, hash);

            if (head == null)
            {
                _db.AuditHashChainHeads.Add(new AuditHashChainHead
                {
                    PartitionKey = partitionKey,
                    Sequence = nextSequence,
                    CurrentHash = hash,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                head.Sequence = nextSequence;
                head.CurrentHash = hash;
                head.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Verifies the entire hot-storage chain from oldest to newest.
        /// Returns (true, count) if intact, (false, firstBrokenId) if tampered.
        /// </summary>
        public async Task<(bool isValid, string details)> VerifyChainAsync(CancellationToken ct = default)
        {
            var entries = await _db.AuditEntries
                .OrderBy(a => a.PerformedAt)
                .AsNoTracking()
                .ToListAsync(ct);

            if (entries.Count == 0)
                return (true, "Chain empty — nothing to verify.");

            string? expectedPrevious = null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var content = BuildContent(entry, expectedPrevious);
                var expectedHash = ComputeSha256(content);

                if (entry.Hash != expectedHash)
                    return (false, $"Chain broken at entry Id={entry.Id} (index {i}). Record may have been tampered.");

                expectedPrevious = entry.Hash;
            }

            return (true, $"Chain intact. {entries.Count} entries verified.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string BuildContent(AuditEntry e, string? previousHash)
        {
            // Deterministic serialization of the record content (excluding Hash/PreviousHash).
            var payload = new
            {
                e.Id,
                e.ServiceName,
                e.Action,
                AuditLevel = e.AuditLevel.ToString(),
                e.EntityName,
                e.EntityId,
                e.PerformedBy,
                e.PerformedAt,
                e.CorrelationId,
                e.TraceId,
                e.BeforeJson,
                e.AfterJson
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return (previousHash ?? "GENESIS") + json;
        }

        private static string ComputeSha256(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash  = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
