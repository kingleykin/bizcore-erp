using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Customer.API.Application.Commands;
using Customer.API.Application.Queries;
using Customer.API.Application.DTOs;
using Customer.API.Application.Clients;
using MediatR;

namespace Customer.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/customer")]
[ApiVersion("1.0")]
[Authorize]
public class CustomersController : ControllerBase
{
    //private readonly IAuditServiceClient _auditClient;
    private readonly IMediator _mediator;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        //IAuditServiceClient auditClient,
        IMediator mediator,
        ILogger<CustomersController> logger)
    {
        //_auditClient = auditClient;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.Customer.View)]
    public async Task<ActionResult<IEnumerable<CustomerResponseDto>>> GetCustomers()
    {
        //_logger.LogInformation("Retrieving all customers");
        var customers = await _mediator.Send(new GetCustomersQuery());
        return Ok(customers);
    }

    [HttpGet("{id}")]
    [RequirePermission(Permissions.Customer.View)]
    public async Task<ActionResult<CustomerResponseDto>> GetCustomer(Guid id)
    {
        //_logger.LogInformation("Retrieving customer CustomerId={CustomerId}", id);
        var customer = await _mediator.Send(new GetCustomerByIdQuery(id));
        if (customer == null)
        {
            //_logger.LogWarning("Customer not found CustomerId={CustomerId}", id);
            return NotFound();
        }
        return Ok(customer);
    }

    [HttpPost]
    [RequirePermission(Permissions.Customer.Create)]
    public async Task<ActionResult<CustomerResponseDto>> CreateCustomer(CreateCustomerRequest request)
    {
        // _logger.LogInformation("Creating customer for FirstName={FirstName}, LastName={LastName}",
        //     request.FirstName, request.LastName);

        var command = new CreateCustomerCommand(request.FirstName, request.LastName, request.Email, request.Phone, request.Address);
        var created = await _mediator.Send(command);

        //_logger.LogInformation("Customer created successfully CustomerId={CustomerId}", created.Id);
        return CreatedAtAction(nameof(GetCustomer), new { id = created.Id }, created);
    }

    // [HttpPut("{id}/status")]
    // [RequirePermission(Permissions.Customer.Update)]
    // public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCustomerStatusRequest request)
    // {
    //     var command = new UpdateCustomerStatusCommand(id, request.Status, request.Version);
    //     var success = await _mediator.Send(command);
    //     if (!success)
    //     {
    //         _logger.LogWarning("Invoice not found or concurrency conflict for status update InvoiceId={InvoiceId}", id);
    //         return NotFound();
    //     }
    //     _logger.LogInformation("Invoice status updated InvoiceId={InvoiceId}, Status={Status}", id, request.Status);
    //     return NoContent();
    // }

    // ── Reversal Endpoints ────────────────────────────────────────────────

    // [HttpGet("{id}/restore-suggestion")]
    // [RequirePermission(Permissions.Audit.View)]
    // public async Task<IActionResult> GetRestoreSuggestion(
    //     Guid id,
    //     [FromQuery] Guid auditEntryId,
    //     CancellationToken ct)
    // {
    //     var query = new GetInvoiceRestoreSuggestionQuery(id, auditEntryId, User);
    //     var suggestion = await _mediator.Send(query, ct);

    //     if (suggestion is null)
    //         return NotFound(new { error = "Không tìm thấy Invoice hoặc AuditEntry hợp lệ." });

    //     return Ok(suggestion);
    // }

    // [HttpPost("{id}/restore-field")]
    // [RequirePermission(Permissions.Audit.View)]
    // public async Task<IActionResult> RestoreField(
    //     Guid id,
    //     [FromBody] RestoreFieldRequest request,
    //     CancellationToken ct)
    // {
    //     if (string.IsNullOrWhiteSpace(request.Reason))
    //         return BadRequest(new { error = "Lý do khôi phục (Reason) là bắt buộc." });

    //     var command = new RestoreInvoiceFieldCommand(
    //         InvoiceId: id,
    //         Field: request.Field,
    //         PreviousValue: request.PreviousValue,
    //         SourceAuditEntryId: request.AuditEntryId,
    //         Reason: request.Reason,
    //         Actor: User);

    //     var result = await _mediator.Send(command, ct);

    //     if (!result.Success)
    //         return BadRequest(new { error = result.Message });

    //     return Ok(new { message = result.Message });
    // }
}
