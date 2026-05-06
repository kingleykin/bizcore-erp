using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Bizcore.BuildingBlocks.Contracts;

namespace Payment.API.Application.Services
{
    public interface IPaymentService
    {
        /// <summary>
        /// Tạo payment record với trạng thái Processing và publish IPaymentInitiatedEvent.
        /// Kết quả cuối cùng (Completed/Failed) sẽ được cập nhật async bởi Saga.
        /// </summary>
        Task<InitiatePaymentResult> InitiatePaymentAsync(
            Payment.API.Domain.Entities.Payment payment,
            string idempotencyKey);

        Task<Payment.API.Domain.Entities.Payment?> GetByIdAsync(Guid paymentId);
        Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync();
    }

    public record InitiatePaymentResult(bool Accepted, Guid? PaymentId, string? ErrorReason);

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IIdempotencyService _idempotencyService;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IIdempotencyService idempotencyService,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _idempotencyService = idempotencyService;
            _logger = logger;
        }

        public async Task<InitiatePaymentResult> InitiatePaymentAsync(
            Payment.API.Domain.Entities.Payment payment,
            string idempotencyKey)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
                return new InitiatePaymentResult(false, null, "Idempotency key is required.");

            // Kiểm tra invoice tồn tại trong read model của Payment service
            var invoiceExists = await _context.Invoices.AnyAsync(i => i.Id == payment.InvoiceId);
            if (!invoiceExists)
            {
                _logger.LogWarning(
                    "Invoice not found in Payment read model InvoiceId={InvoiceId}",
                    payment.InvoiceId);
                return new InitiatePaymentResult(false, null, "Invoice not found.");
            }

            // Generate PaymentId trước khi check idempotency
            var paymentId = Guid.NewGuid();

            // Check idempotency với database-backed implementation
            var idempotencyCheck = await _idempotencyService.CheckOrCreateAsync(
                idempotencyKey,
                new { payment.InvoiceId, payment.Amount }, // Request payload for hash
                paymentId,
                TimeSpan.FromMinutes(30));

            if (!idempotencyCheck.IsNew)
            {
                if (idempotencyCheck.ConflictReason != null)
                {
                    _logger.LogWarning(
                        "Idempotency conflict IdempotencyKey={Key}: {Reason}",
                        idempotencyKey, idempotencyCheck.ConflictReason);
                    return new InitiatePaymentResult(false, null, idempotencyCheck.ConflictReason);
                }

                _logger.LogInformation(
                    "Duplicate request detected IdempotencyKey={Key} PaymentId={PaymentId}",
                    idempotencyKey, idempotencyCheck.PaymentId);
                return new InitiatePaymentResult(true, idempotencyCheck.PaymentId, null);
            }

            // Tạo payment với trạng thái Processing — chưa commit tiền
            payment.Id = paymentId;
            payment.PaymentDate = DateTime.UtcNow;
            payment.Status = PaymentStatus.Processing;
            payment.IdempotencyKey = idempotencyKey;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Payment created with Processing status PaymentId={PaymentId} InvoiceId={InvoiceId}",
                payment.Id, payment.InvoiceId);

            // Publish event để Saga orchestrator bắt đầu điều phối
            await _publishEndpoint.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                IdempotencyKey = idempotencyKey,
                InitiatedAt = payment.PaymentDate
            });

            return new InitiatePaymentResult(true, payment.Id, null);
        }

        public async Task<Payment.API.Domain.Entities.Payment?> GetByIdAsync(Guid paymentId)
            => await _context.Payments.FindAsync(paymentId);

        public async Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync()
            => await _context.Payments.ToListAsync();
    }
}
