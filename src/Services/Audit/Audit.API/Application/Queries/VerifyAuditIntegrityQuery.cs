using Audit.API.Application.DTOs;
using Audit.API.Infrastructure.Services;
using MediatR;

namespace Audit.API.Application.Queries;

public record VerifyAuditIntegrityQuery : IRequest<IntegrityResult>;

public class VerifyAuditIntegrityHandler : IRequestHandler<VerifyAuditIntegrityQuery, IntegrityResult>
{
    private readonly HashChainService _hashChain;

    public VerifyAuditIntegrityHandler(HashChainService hashChain)
    {
        _hashChain = hashChain;
    }

    public async Task<IntegrityResult> Handle(VerifyAuditIntegrityQuery request, CancellationToken ct)
    {
        var (isValid, details) = await _hashChain.VerifyChainAsync(ct);
        return new IntegrityResult(isValid, details, DateTime.UtcNow);
    }
}
