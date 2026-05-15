using Payment.API.Application.Commands;
using Payment.API.Application.Queries;
using Payment.API.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;

namespace Payment.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/payment")]
[ApiVersion("1.0")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMediator mediator,
        ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Khởi tạo payment với trạng thái Processing.
    /// </summary>
    [HttpPost("pay")]
    [RequirePermission(Permissions.Payment.Create)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest(new { Error = "Missing X-Idempotency-Key header" });

        var command = new InitiatePaymentCommand(
            request.InvoiceId,
            request.Amount,
            request.PaymentMethod,
            idempotencyKey
        );

        var result = await _mediator.Send(command);

        if (!result.Accepted)
            return BadRequest(new { Error = result.ErrorReason });

        if (result.CachedResponse != null)
            return StatusCode(result.StatusCode ?? 202, result.CachedResponse);

        return AcceptedAtAction(
            nameof(GetPaymentById),
            new { version = "1.0", id = result.PaymentId },
            new
            {
                PaymentId = result.PaymentId,
                Status = "Processing",
                Message = "Payment is being processed. Poll this endpoint to get the final status."
            });
    }

    /// <summary>
    /// Lấy thông tin payment theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(Permissions.Payment.View)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentById(Guid id)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery(id));
        return result == null ? NotFound(new { Error = "Payment not found" }) : Ok(result);
    }

    /// <summary>
    /// Lấy danh sách tất cả payments.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Payment.View)]
    [ProducesResponseType(typeof(IEnumerable<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments()
    {
        var result = await _mediator.Send(new GetPaymentsQuery());
        return Ok(result);
    }
}
