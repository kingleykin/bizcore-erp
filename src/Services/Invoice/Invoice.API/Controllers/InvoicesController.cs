using Invoice.API.Application.Services;
using Invoice.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/invoice")]
    [ApiVersion("1.0")]
    [Authorize(Policy = "Invoice.View")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
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

            // FluentValidation handled by middleware
            var invoice = Invoice.API.Domain.Entities.Invoice.Create(request.CustomerName, request.Amount);
            var created = await _invoiceService.CreateAsync(invoice);

            _logger.LogInformation("Invoice created successfully InvoiceId={InvoiceId}", created.Id);
            return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
        }

        [HttpPut("{id}/status")]
        [Authorize(Policy = "Invoice.Update")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] InvoiceStatus status)
        {
            _logger.LogInformation("Updating invoice status InvoiceId={InvoiceId}, Status={Status}", id, status);
            var success = await _invoiceService.UpdateStatusAsync(id, status);
            if (!success)
            {
                _logger.LogWarning("Invoice not found for status update InvoiceId={InvoiceId}", id);
                return NotFound();
            }
            _logger.LogInformation("Invoice status updated InvoiceId={InvoiceId}, Status={Status}", id, status);
            return NoContent();
        }
    }
}
