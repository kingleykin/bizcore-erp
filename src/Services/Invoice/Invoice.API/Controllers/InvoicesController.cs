using Invoice.API.Application.Services;
using Invoice.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Invoice.API.Controllers
{
    [ApiController]
    [Route("invoice")]
    [Authorize(Policy = "Invoice.View")] // Default policy for the whole controller
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice.API.Domain.Entities.Invoice>>> GetInvoices()
        {
            var invoices = await _invoiceService.GetAllAsync();
            return Ok(invoices);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice.API.Domain.Entities.Invoice>> GetInvoice(Guid id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null) return NotFound();
            return Ok(invoice);
        }

        [HttpPost]
        [Authorize(Policy = "Invoice.Create")]
        public async Task<ActionResult<Invoice.API.Domain.Entities.Invoice>> CreateInvoice(Invoice.API.DTOs.CreateInvoiceRequest request)
        {
            // FluentValidation handled by middleware
            var invoice = Invoice.API.Domain.Entities.Invoice.Create(request.CustomerName, request.Amount);
            var created = await _invoiceService.CreateAsync(invoice);
            return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
        }

        [HttpPut("{id}/status")]
        [Authorize(Policy = "Invoice.Update")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] InvoiceStatus status)
        {
            var success = await _invoiceService.UpdateStatusAsync(id, status);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
