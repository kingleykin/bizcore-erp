using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Bizcore.BuildingBlocks.Contracts;

namespace Payment.API.Application.Services
{
    public interface IPaymentService
    {
        Task<bool> ProcessPaymentAsync(Payment.API.Domain.Entities.Payment payment);
        Task<IEnumerable<Payment.API.Domain.Entities.Payment>> GetAllAsync();
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public PaymentService(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<bool> ProcessPaymentAsync(Payment.API.Domain.Entities.Payment payment)
        {
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
