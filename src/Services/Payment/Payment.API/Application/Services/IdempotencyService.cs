using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Services
{
    public interface IIdempotencyService
    {
        /// <summary>
        /// Check idempotency key và trả về existing PaymentId nếu đã tồn tại.
        /// Nếu chưa tồn tại, tạo record mới trong transaction.
        /// </summary>
        Task<IdempotencyCheckResult> CheckOrCreateAsync(
            string idempotencyKey,
            object requestPayload,
            Guid paymentId,
            TimeSpan ttl);

        /// <summary>
        /// Cache response for idempotency replay.
        /// </summary>
        Task CacheResponseAsync(
            string idempotencyKey,
            object response,
            int statusCode = 200);

        /// <summary>
        /// Cleanup expired idempotency records (gọi từ background job).
        /// </summary>
        Task<int> CleanupExpiredRecordsAsync(CancellationToken cancellationToken = default);
    }

    public record IdempotencyCheckResult(
        bool IsNew,
        Guid PaymentId,
        string? ConflictReason = null,
        object? CachedResponse = null,
        int? StatusCode = null);

    public class IdempotencyService : IIdempotencyService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<IdempotencyService> _logger;

        public IdempotencyService(AppDbContext context, ILogger<IdempotencyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IdempotencyCheckResult> CheckOrCreateAsync(
            string idempotencyKey,
            object requestPayload,
            Guid paymentId,
            TimeSpan ttl)
        {
            // Validate key format
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Idempotency key cannot be empty", nameof(idempotencyKey));

            if (idempotencyKey.Length > 256)
                throw new ArgumentException("Idempotency key too long (max 256 chars)", nameof(idempotencyKey));

            var requestHash = ComputeRequestHash(requestPayload);

            // Try to find existing record
            var existing = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

            if (existing != null)
            {
                // Check if expired
                if (existing.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning(
                        "Idempotency key expired Key={Key} ExpiresAt={ExpiresAt}",
                        idempotencyKey, existing.ExpiresAt);

                    // Remove expired record và cho phép tạo mới
                    _context.IdempotencyRecords.Remove(existing);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Verify request consistency
                    if (existing.RequestHash != requestHash)
                    {
                        _logger.LogWarning(
                            "Idempotency key conflict: same key, different payload Key={Key}",
                            idempotencyKey);

                        return new IdempotencyCheckResult(
                            false,
                            existing.PaymentId,
                            "Idempotency key already used with different request payload");
                    }

                    _logger.LogInformation(
                        "Duplicate request detected Key={Key} PaymentId={PaymentId}",
                        idempotencyKey, existing.PaymentId);

                    // Deserialize cached response if available
                    object? cachedResponse = null;
                    if (!string.IsNullOrEmpty(existing.ResponseJson))
                    {
                        try
                        {
                            cachedResponse = JsonSerializer.Deserialize<object>(existing.ResponseJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize cached response for Key={Key}", idempotencyKey);
                        }
                    }

                    return new IdempotencyCheckResult(
                        false,
                        existing.PaymentId,
                        null,
                        cachedResponse,
                        existing.StatusCode
                    );
                }
            }

            // Create new record
            var record = new IdempotencyRecord
            {
                Key = idempotencyKey,
                PaymentId = paymentId,
                RequestHash = requestHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                Status = "InProgress"
            };

            try
            {
                _context.IdempotencyRecords.Add(record);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Idempotency record created Key={Key} PaymentId={PaymentId} ExpiresAt={ExpiresAt}",
                    idempotencyKey, paymentId, record.ExpiresAt);

                return new IdempotencyCheckResult(true, paymentId);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Race condition: another thread created record between our check and insert
                _logger.LogWarning(
                    "Race condition detected for idempotency key Key={Key}",
                    idempotencyKey);

                // Re-query to get the winning record
                var winner = await _context.IdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

                if (winner != null)
                {
                    return new IdempotencyCheckResult(false, winner.PaymentId);
                }

                // Shouldn't happen, but rethrow if we can't find the record
                throw;
            }
        }

        public async Task CacheResponseAsync(
            string idempotencyKey,
            object response,
            int statusCode = 200)
        {
            var record = await _context.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

            if (record != null)
            {
                record.ResponseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                record.StatusCode = statusCode;
                record.Status = statusCode < 500 ? "Completed" : "Failed";

                // Note: Do NOT call SaveChangesAsync here when called inside a TransactionBehavior flow.
                // The UnitOfWork.CommitAsync will persist the cached response with the business changes.
                // If called outside transaction context (e.g., from a background job), SaveChangesAsync is needed.
                // For now, we rely on the transaction pipeline to save.
            }
        }

        public async Task<int> CleanupExpiredRecordsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow;

            var expiredRecords = await _context.IdempotencyRecords
                .Where(r => r.ExpiresAt < cutoff)
                .ToListAsync(cancellationToken);

            if (expiredRecords.Count == 0)
                return 0;

            _context.IdempotencyRecords.RemoveRange(expiredRecords);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cleaned up {Count} expired idempotency records",
                expiredRecords.Count);

            return expiredRecords.Count;
        }

        private static string ComputeRequestHash(object payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // SQL Server unique constraint violation
            return ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
                || ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
