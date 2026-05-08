using Payment.API.Application.Services;
using Payment.API.Application.Commands;
using Payment.API.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/payment")]
    [ApiVersion("1.0")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IMediator mediator,
            IPaymentService paymentService,
            ILogger<PaymentsController> logger)
        {
            _mediator = mediator;
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Khởi tạo payment với trạng thái Processing.
        /// Kết quả cuối cùng (Completed/Failed) sẽ được cập nhật async bởi Saga orchestrator.
        /// Client cần poll GET /payment/{id} để lấy trạng thái cuối.
        /// </summary>
        [HttpPost("pay")]
        [RequirePermission(Permissions.Payment.Create)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ProcessPayment(
            [FromBody] ProcessPaymentRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
        {
            _logger.LogInformation(
                "Processing payment request InvoiceId={InvoiceId} Amount={Amount}",
                request.InvoiceId, request.Amount);

            if (string.IsNullOrEmpty(idempotencyKey))
            {
                _logger.LogWarning("Payment request rejected: Missing X-Idempotency-Key header");
                return BadRequest(new { Error = "Missing X-Idempotency-Key header" });
            }

            // Use MediatR command with transaction pipeline
            var command = new InitiatePaymentCommand(
                request.InvoiceId,
                request.Amount,
                request.PaymentMethod,
                idempotencyKey
            );

            var result = await _mediator.Send(command);

            if (!result.Accepted)
            {
                _logger.LogWarning(
                    "Payment initiation failed InvoiceId={InvoiceId}: {Reason}",
                    request.InvoiceId, result.ErrorReason);
                return BadRequest(new { Error = result.ErrorReason });
            }

            // If cached response exists (duplicate request), return it with same status code
            if (result.CachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached response for duplicate request. PaymentId={PaymentId}",
                    result.PaymentId);
                return StatusCode(result.StatusCode ?? 202, result.CachedResponse);
            }

            _logger.LogInformation(
                "Payment initiated successfully PaymentId={PaymentId} Status=Processing",
                result.PaymentId);

            // 202 Accepted: payment đã được tạo nhưng chưa hoàn tất
            // Client cần poll GET /payment/{id} để lấy trạng thái cuối
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
        /// Lấy thông tin payment theo ID, bao gồm trạng thái hiện tại.
        /// Client dùng endpoint này để poll trạng thái sau khi POST /pay.
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.Payment.View)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentStatusResponse>> GetPaymentById(Guid id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment == null)
            {
                return NotFound(new { Error = "Payment not found" });
            }

            var response = new PaymentStatusResponse
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                PaymentDate = payment.PaymentDate,
                FailureReason = payment.FailureReason
            };

            // Nếu đang Processing, tính TTL và gợi ý retry
            if (payment.Status == PaymentStatus.Processing)
            {
                var elapsed = (DateTime.UtcNow - payment.PaymentDate).TotalSeconds;
                var timeout = 60; // Saga timeout = 60 giây
                response.ExpiresIn = Math.Max(0, (int)(timeout - elapsed));

                // Exponential backoff: 2s → 5s → 10s
                response.RetryAfter = elapsed switch
                {
                    < 10 => 2,
                    < 30 => 5,
                    _ => 10
                };
            }

            return Ok(response);
        }

        /// <summary>
        /// Lấy danh sách tất cả payments (dùng cho admin/debug).
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.Payment.View)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Payment.API.Domain.Entities.Payment>>> GetPayments()
        {
            _logger.LogInformation("Retrieving all payments");
            var payments = await _paymentService.GetAllAsync();
            _logger.LogInformation("Retrieved {Count} payments", payments.Count());
            return Ok(payments);
        }
    }

    public class ProcessPaymentRequest
    {
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class PaymentStatusResponse
    {
        public Guid PaymentId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public string? FailureReason { get; set; }
        
        /// <summary>
        /// Số giây còn lại trước khi payment timeout (chỉ có khi Status = Processing).
        /// Client nên dừng poll khi expiresIn <= 0.
        /// </summary>
        public int? ExpiresIn { get; set; }
        
        /// <summary>
        /// Gợi ý thời gian (giây) client nên đợi trước khi poll lần tiếp theo.
        /// Exponential backoff: 2s → 5s → 10s.
        /// </summary>
        public int? RetryAfter { get; set; }
    }
}
