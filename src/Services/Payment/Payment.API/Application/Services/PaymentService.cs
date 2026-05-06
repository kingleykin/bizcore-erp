using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Bizcore.BuildingBlocks.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace Payment.API.Application.Services
{
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(Payment.API.Domain.Entities.Payment payment, string idempotencyKey);
        Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync();
    }

    public record PaymentResult(bool Success, string? ErrorReason = null);

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IRequestClient<IApplyPaymentToInvoiceRequest> _applyPaymentClient;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            AppDbContext context,
            IRequestClient<IApplyPaymentToInvoiceRequest> applyPaymentClient,
            IPublishEndpoint publishEndpoint,
            IMemoryCache cache,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _applyPaymentClient = applyPaymentClient;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(
            Payment.API.Domain.Entities.Payment payment,
            string idempotencyKey)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
                return new PaymentResult(false, "Idempotency key is required.");

            // Idempotency: request đã xử lý thành công trước đó
            if (_cache.TryGetValue(idempotencyKey, out _))
            {
                _logger.LogInformation("Duplicate request detected for IdempotencyKey={Key}", idempotencyKey);
                return new PaymentResult(true);
            }

            // Kiểm tra invoice tồn tại trong read model của Payment service
            var invoiceExists = await _context.Invoices.AnyAsync(i => i.Id == payment.InvoiceId);
            if (!invoiceExists)
            {
                _logger.LogWarning("Invoice not found in Payment read model InvoiceId={InvoiceId}", payment.InvoiceId);
                return new PaymentResult(false, "Invoice not found.");
            }

            payment.Id = Guid.NewGuid();
            payment.PaymentDate = DateTime.UtcNow;
            payment.Status = PaymentStatus.Completed;

            // Request-Reply: yêu cầu Invoice service xác nhận và cập nhật trạng thái
            // Đợi kết quả trước khi commit payment — nếu Invoice từ chối thì không lưu gì cả
            _logger.LogInformation(
                "Sending ApplyPayment request to Invoice service PaymentId={PaymentId} InvoiceId={InvoiceId}",
                payment.Id, payment.InvoiceId);

            Response<IApplyPaymentToInvoiceResponse> response;
            try
            {
                response = await _applyPaymentClient.GetResponse<IApplyPaymentToInvoiceResponse>(new
                {
                    PaymentId = payment.Id,
                    InvoiceId = payment.InvoiceId,
                    Amount = payment.Amount
                });
            }
            catch (RequestTimeoutException ex)
            {
                _logger.LogError(ex,
                    "Timeout waiting for Invoice service response PaymentId={PaymentId}", payment.Id);
                return new PaymentResult(false, "Invoice service did not respond in time. Please try again.");
            }

            if (!response.Message.Success)
            {
                _logger.LogWarning(
                    "Invoice service rejected payment PaymentId={PaymentId} InvoiceId={InvoiceId}: {Reason}",
                    payment.Id, payment.InvoiceId, response.Message.ErrorReason);
                return new PaymentResult(false, response.Message.ErrorReason);
            }

            // Invoice đã xác nhận → commit payment
            _context.Payments.Add(payment);
            _cache.Set(idempotencyKey, true, TimeSpan.FromMinutes(30));

            // Publish event để Report và Orchestration cập nhật read model của họ
            await _publishEndpoint.Publish<IPaymentCompletedEvent>(new
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Payment committed successfully PaymentId={PaymentId} InvoiceId={InvoiceId}",
                payment.Id, payment.InvoiceId);

            return new PaymentResult(true);
        }

        public async Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync()
        {
            return await _context.Payments.ToListAsync();
        }
    }
}
