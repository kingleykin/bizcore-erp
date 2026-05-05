using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("payment")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("pay")]
        public async Task<IActionResult> ProcessPayment([FromBody] Payment.API.Domain.Entities.Payment payment)
        {
            var success = await _paymentService.ProcessPaymentAsync(payment);
            if (!success) return NotFound("Invoice not found");

            return Ok(new { Message = "Payment processed successfully", PaymentId = payment.Id });
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment.API.Domain.Entities.Payment>>> GetPayments()
        {
            var payments = await _paymentService.GetAllAsync();
            return Ok(payments);
        }
    }
}
