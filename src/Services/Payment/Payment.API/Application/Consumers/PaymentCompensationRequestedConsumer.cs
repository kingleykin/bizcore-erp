using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    public class PaymentCompensationRequestedConsumer : IConsumer<IPaymentCompensationRequestedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentCompensationRequestedConsumer> _logger;

        public PaymentCompensationRequestedConsumer(AppDbContext context, ILogger<PaymentCompensationRequestedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
        {
            var message = context.Message;

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == message.PaymentId);
            if (payment == null)
            {
                _logger.LogWarning(
                    "Compensation requested but payment not found. PaymentId: {PaymentId}, InvoiceId: {InvoiceId}",
                    message.PaymentId,
                    message.InvoiceId);
                return;
            }

            if (payment.Status == PaymentStatus.Reversed)
            {
                _logger.LogInformation("Payment {PaymentId} is already reversed. Skipping.", message.PaymentId);
                return;
            }

            payment.Status = PaymentStatus.Reversed;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Payment reversed successfully. PaymentId: {PaymentId}, InvoiceId: {InvoiceId}, Reason: {Reason}",
                message.PaymentId,
                message.InvoiceId,
                message.Reason);
        }
    }
}
