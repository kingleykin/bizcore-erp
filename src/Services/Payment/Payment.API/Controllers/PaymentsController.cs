using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/payment")]
    [ApiVersion("1.0")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost("pay")]
        public async Task<IActionResult> ProcessPayment([FromBody] Payment.API.Domain.Entities.Payment payment, [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
        {
            _logger.LogInformation("Processing payment request for InvoiceId={InvoiceId}, Amount={Amount}", 
                payment.InvoiceId, payment.Amount);

            if (string.IsNullOrEmpty(idempotencyKey))
            {
                _logger.LogWarning("Payment request rejected: Missing X-Idempotency-Key header");
                return BadRequest("Missing X-Idempotency-Key header");
            }

            var result = await _paymentService.ProcessPaymentAsync(payment, idempotencyKey);
            if (!result.Success)
            {
                _logger.LogWarning("Payment processing failed for InvoiceId={InvoiceId}: {Reason}", 
                    payment.InvoiceId, result.ErrorReason);
                return BadRequest(new { Error = result.ErrorReason });
            }

            _logger.LogInformation("Payment processed successfully for InvoiceId={InvoiceId}", payment.InvoiceId);
            return Ok(new { Message = "Payment processed successfully" });
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment.API.Domain.Entities.Payment>>> GetPayments()
        {
            _logger.LogInformation("Retrieving all payments");
            var payments = await _paymentService.GetAllAsync();
            _logger.LogInformation("Retrieved {Count} payments", payments.Count());
            return Ok(payments);
        }
    }
}
