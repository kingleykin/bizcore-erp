using Asp.Versioning;
using Audit.API.Application.DTOs;
using Audit.API.Application.Commands;
using Audit.API.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Audit.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Query audit entries with full filtering and pagination.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Audit.View")]
    public async Task<IActionResult> Query([FromQuery] AuditQueryParams q, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAuditEntriesQuery(q), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a single audit entry by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Audit.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAuditEntryByIdQuery(id), ct);
        return result is null ? NotFound(new { error = $"Audit entry '{id}' not found." }) : Ok(result);
    }

    /// <summary>
    /// Verify the SHA-256 hash chain integrity of the hot audit store.
    /// </summary>
    [HttpGet("verify-integrity")]
    [Authorize(Policy = "Audit.View")]
    public async Task<IActionResult> VerifyIntegrity(CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyAuditIntegrityQuery(), ct);
        return result.IsValid ? Ok(result) : StatusCode(500, result);
    }

    /// <summary>
    /// Mark an audit entry as reversed.
    /// </summary>
    [HttpPatch("{id:guid}/mark-reversed")]
    [Authorize(Policy = "Audit.View")]
    public async Task<IActionResult> MarkReversed(Guid id, [FromBody] MarkReversedRequest request, CancellationToken ct)
    {
        await _mediator.Send(new MarkAuditReversedCommand(id, request.ReversalEntryId, request.Reason), ct);
        return Ok(new { message = "AuditEntry đã được đánh dấu reversed." });
    }
}
