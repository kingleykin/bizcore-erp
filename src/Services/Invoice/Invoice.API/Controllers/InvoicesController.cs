using Invoice.API.Application.Clients;
using Invoice.API.Application.Policies;
using Invoice.API.Application.Services;
using Invoice.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Reversal;

namespace Invoice.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/invoice")]
    [ApiVersion("1.0")]
    [Authorize(Policy = "Invoice.View")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService      _invoiceService;
        private readonly IAuditServiceClient  _auditClient;
        private readonly MediatR.IMediator    _mediator;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(
            IInvoiceService      invoiceService,
            IAuditServiceClient  auditClient,
            MediatR.IMediator    mediator,
            ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _auditClient    = auditClient;
            _mediator       = mediator;
            _logger         = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice.API.Domain.Entities.Invoice>>> GetInvoices()
        {
            _logger.LogInformation("Retrieving all invoices");
            var invoices = await _invoiceService.GetAllAsync();
            _logger.LogInformation("Retrieved {Count} invoices", invoices.Count());
            return Ok(invoices);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice.API.Domain.Entities.Invoice>> GetInvoice(Guid id)
        {
            _logger.LogInformation("Retrieving invoice InvoiceId={InvoiceId}", id);
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice not found InvoiceId={InvoiceId}", id);
                return NotFound();
            }
            return Ok(invoice);
        }

        [HttpPost]
        [Authorize(Policy = "Invoice.Create")]
        public async Task<ActionResult<Invoice.API.Domain.Entities.Invoice>> CreateInvoice(Invoice.API.DTOs.CreateInvoiceRequest request)
        {
            _logger.LogInformation("Creating invoice for CustomerName={CustomerName}, Amount={Amount}",
                request.CustomerName, request.Amount);

            var command = new Invoice.API.Application.Commands.CreateInvoiceCommand(request.CustomerName, request.Amount);
            var created = await _mediator.Send(command);

            _logger.LogInformation("Invoice created successfully InvoiceId={InvoiceId}", created.Id);
            return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
        }

        [HttpPut("{id}/status")]
        [Authorize(Policy = "Invoice.Update")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] InvoiceStatus status)
        {
            var command = new Invoice.API.Application.Commands.UpdateInvoiceStatusCommand(id, status);
            var success = await _mediator.Send(command);
            if (!success)
            {
                _logger.LogWarning("Invoice not found for status update InvoiceId={InvoiceId}", id);
                return NotFound();
            }
            _logger.LogInformation("Invoice status updated InvoiceId={InvoiceId}, Status={Status}", id, status);
            return NoContent();
        }

        // ── Reversal Endpoints ────────────────────────────────────────────────

        /// <summary>
        /// Trả về diff giữa BeforeJson (AuditEntry) và trạng thái hiện tại.
        /// Cho Admin biết field nào có thể Restore, field nào cần Manual Action.
        /// </summary>
        [HttpGet("{id}/restore-suggestion")]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> GetRestoreSuggestion(
            Guid  id,
            [FromQuery] Guid auditEntryId,
            CancellationToken ct)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice is null) return NotFound(new { error = $"Invoice '{id}' không tồn tại." });

            var auditEntry = await _auditClient.GetEntryAsync(auditEntryId, ct);
            if (auditEntry?.BeforeJson is null)
                return NotFound(new { error = "Không tìm thấy AuditEntry hoặc không có dữ liệu BeforeJson." });

            if (auditEntry.EntityId != id.ToString())
                return BadRequest(new { error = "AuditEntry này không thuộc về Invoice đang truy vấn." });

            var suggestion = RestoreDiffEngine.ComputeDiff(
                auditEntryId    : auditEntryId,
                entityType      : "Invoice",
                entityId        : id.ToString(),
                changedAt       : auditEntry.PerformedAt,
                originalAction  : auditEntry.Action,
                beforeJson      : auditEntry.BeforeJson,
                currentEntity   : invoice,
                policy          : new InvoiceReversalPolicy(),
                actor           : User);

            return Ok(suggestion);
        }

        /// <summary>
        /// Thực hiện restore một field cụ thể về giá trị cũ.
        /// Yêu cầu lý do bắt buộc. Tạo AuditEntry mới ghi nhận hành động.
        /// </summary>
        [HttpPost("{id}/restore-field")]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> RestoreField(
            Guid id,
            [FromBody] RestoreFieldRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "Lý do khôi phục (Reason) là bắt buộc." });

            // Kiểm tra policy trước khi thực hiện
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice is null) return NotFound();

            var policy   = new InvoiceReversalPolicy();
            var decision = policy.CanRestore(request.Field, invoice, User);
            if (!decision.IsAllowed)
                return StatusCode(403, new { error = decision.Reason });

            var command = new Invoice.API.Application.Commands.RestoreInvoiceFieldCommand(
                InvoiceId         : id,
                Field             : request.Field,
                PreviousValue     : request.PreviousValue,
                SourceAuditEntryId: request.AuditEntryId,
                Reason            : request.Reason,
                Actor             : User);

            var result = await _mediator.Send(command, ct);

            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new { message = result.Message });
        }
    }

    public record RestoreFieldRequest(
        string Field,
        string PreviousValue,
        Guid   AuditEntryId,
        string Reason
    );
}
