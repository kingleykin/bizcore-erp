using Audit.API.Application.Services;

namespace Audit.API.Application.Jobs
{
    /// <summary>
    /// Hangfire weekly job: verifies the hash chain on hot AuditEntries.
    /// Logs a warning if tampering is detected.
    /// </summary>
    public class IntegrityVerificationJob
    {
        private readonly IAuditQueryService _queryService;
        private readonly ILogger<IntegrityVerificationJob> _logger;

        public IntegrityVerificationJob(
            IAuditQueryService queryService,
            ILogger<IntegrityVerificationJob> logger)
        {
            _queryService = queryService;
            _logger       = logger;
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("IntegrityVerificationJob started.");

            var result = await _queryService.VerifyIntegrityAsync(ct);

            if (result.IsValid)
                _logger.LogInformation("Audit chain integrity OK. {Details}", result.Details);
            else
                _logger.LogCritical(
                    "⚠ AUDIT CHAIN INTEGRITY VIOLATION DETECTED at {CheckedAt}! {Details}",
                    result.CheckedAt, result.Details);
        }
    }
}
