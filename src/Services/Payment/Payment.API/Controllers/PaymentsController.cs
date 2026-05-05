using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/payment")]
    [ApiVersion("1.0")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("pay")]
        public async Task<IActionResult> ProcessPayment([FromBody] Payment.API.Domain.Entities.Payment payment, [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey)
        {
            var result = await _paymentService.ProcessPaymentAsync(payment, idempotencyKey);
            if (!result) return BadRequest("Payment processing failed or Idempotency Key missing");
            return Ok(new { Message = "Payment processed and event published" });
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment.API.Domain.Entities.Payment>>> GetPayments()
        {
            var payments = await _paymentService.GetAllAsync();
            return Ok(payments);
        }
    }
}
