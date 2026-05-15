using Invoice.API.Application.Clients;
using Invoice.API.Application.DTOs;
using Invoice.API.Application.Queries;
using Invoice.API.Application.Commands;
using Invoice.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;

namespace Invoice.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/invoice")]
[ApiVersion("1.0")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IAuditServiceClient _auditClient;
    private readonly IMediator _mediator;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        IAuditServiceClient auditClient,
        IMediator mediator,
        ILogger<InvoicesController> logger)
    {
        _auditClient = auditClient;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.Invoice.View)]
    public async Task<ActionResult<IEnumerable<InvoiceResponseDto>>> GetInvoices()
    {
        _logger.LogInformation("Retrieving all invoices");
        var invoices = await _mediator.Send(new GetInvoicesQuery());
        return Ok(invoices);
    }

    [HttpGet("{id}")]
    [RequirePermission(Permissions.Invoice.View)]
    public async Task<ActionResult<InvoiceResponseDto>> GetInvoice(Guid id)
    {
        _logger.LogInformation("Retrieving invoice InvoiceId={InvoiceId}", id);
        var invoice = await _mediator.Send(new GetInvoiceByIdQuery(id));
        if (invoice == null)
        {
            _logger.LogWarning("Invoice not found InvoiceId={InvoiceId}", id);
            return NotFound();
        }
        return Ok(invoice);
    }

    [HttpPost]
    [RequirePermission(Permissions.Invoice.Create)]
    public async Task<ActionResult<InvoiceResponseDto>> CreateInvoice(CreateInvoiceRequest request)
    {
        _logger.LogInformation("Creating invoice for CustomerName={CustomerName}, Amount={Amount}",
            request.CustomerName, request.Amount);

        var command = new CreateInvoiceCommand(request.CustomerName, request.Amount);
        var created = await _mediator.Send(command);

        _logger.LogInformation("Invoice created successfully InvoiceId={InvoiceId}", created.Id);
        return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
    }

    [HttpPut("{id}/status")]
    [RequirePermission(Permissions.Invoice.Update)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInvoiceStatusRequest request)
    {
        var command = new UpdateInvoiceStatusCommand(id, request.Status, request.Version);
        var success = await _mediator.Send(command);
        if (!success)
        {
            _logger.LogWarning("Invoice not found or concurrency conflict for status update InvoiceId={InvoiceId}", id);
            return NotFound();
        }
        _logger.LogInformation("Invoice status updated InvoiceId={InvoiceId}, Status={Status}", id, request.Status);
        return NoContent();
    }

    // ── Reversal Endpoints ────────────────────────────────────────────────

    [HttpGet("{id}/restore-suggestion")]
    [RequirePermission(Permissions.Audit.View)]
    public async Task<IActionResult> GetRestoreSuggestion(
        Guid id,
        [FromQuery] Guid auditEntryId,
        CancellationToken ct)
    {
        var query = new GetInvoiceRestoreSuggestionQuery(id, auditEntryId, User);
        var suggestion = await _mediator.Send(query, ct);

        if (suggestion is null)
            return NotFound(new { error = "Không tìm thấy Invoice hoặc AuditEntry hợp lệ." });

        return Ok(suggestion);
    }

    [HttpPost("{id}/restore-field")]
    [RequirePermission(Permissions.Audit.View)]
    public async Task<IActionResult> RestoreField(
        Guid id,
        [FromBody] RestoreFieldRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Lý do khôi phục (Reason) là bắt buộc." });

        var command = new RestoreInvoiceFieldCommand(
            InvoiceId: id,
            Field: request.Field,
            PreviousValue: request.PreviousValue,
            SourceAuditEntryId: request.AuditEntryId,
            Reason: request.Reason,
            Actor: User);

        var result = await _mediator.Send(command, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Message });

        return Ok(new { message = result.Message });
    }
}
