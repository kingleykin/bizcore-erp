using Audit.API.Application.Queries;
using MediatR;

namespace Audit.API.Application.Jobs;

/// <summary>
/// Hangfire weekly job: verifies the hash chain on hot AuditEntries.
/// Logs a warning if tampering is detected.
/// </summary>
public class IntegrityVerificationJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<IntegrityVerificationJob> _logger;

    public IntegrityVerificationJob(IMediator mediator, ILogger<IntegrityVerificationJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("IntegrityVerificationJob started.");

        var result = await _mediator.Send(new VerifyAuditIntegrityQuery(), ct);

        if (result.IsValid)
        {
            _logger.LogInformation("Audit chain integrity OK. {Details}", result.Details);
        }
        else
        {
            _logger.LogCritical(
                "⚠ AUDIT CHAIN INTEGRITY VIOLATION DETECTED at {CheckedAt}! {Details}",
                result.CheckedAt, result.Details);
        }
    }
}
