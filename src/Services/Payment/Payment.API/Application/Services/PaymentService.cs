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
        Task<bool> ProcessPaymentAsync(Payment.API.Domain.Entities.Payment payment, string idempotencyKey);
        Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync();
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IMemoryCache _cache;

        public PaymentService(AppDbContext context, IPublishEndpoint publishEndpoint, IMemoryCache cache)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
        }

        public async Task<bool> ProcessPaymentAsync(Payment.API.Domain.Entities.Payment payment, string idempotencyKey)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) return false;

            if (_cache.TryGetValue(idempotencyKey, out _))
            {
                return true;
            }

            // Note: In event-driven, Payment service might not even need the Invoices table
            // But for this demo, we check if invoice exists in shared DB
            var invoiceExists = await _context.Invoices.AnyAsync(i => i.Id == payment.InvoiceId);
            if (!invoiceExists) return false;

            payment.Id = Guid.NewGuid();
            payment.PaymentDate = DateTime.UtcNow;
            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            // Publish Event to RabbitMQ
            await _publishEndpoint.Publish<IPaymentCompletedEvent>(new
            {
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate
            });

            return true;
        }

        public async Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync()
        {
            return await _context.Payments.ToListAsync();
        }
    }
}
