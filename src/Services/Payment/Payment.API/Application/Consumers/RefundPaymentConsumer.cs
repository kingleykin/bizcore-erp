using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    public class RefundPaymentConsumer : IConsumer<IRefundPaymentCommand>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RefundPaymentConsumer> _logger;

        public RefundPaymentConsumer(AppDbContext context, ILogger<RefundPaymentConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IRefundPaymentCommand> context)
        {
            var message = context.Message;
            _logger.LogInformation("Processing RefundPaymentCommand for PaymentId={PaymentId}, Reason={Reason}", message.PaymentId, message.Reason);

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == message.PaymentId, context.CancellationToken);
            if (payment == null)
            {
                _logger.LogWarning("Payment {PaymentId} not found to refund.", message.PaymentId);
                return;
            }

            // Update state
            payment.Status = Domain.Entities.PaymentStatus.Failed; // Or Refunded/Cancelled if you have that status
            payment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("Payment {PaymentId} has been successfully refunded.", message.PaymentId);

            // Publish confirmation event back to Saga
            await context.Publish<IPaymentRefundedEvent>(new
            {
                PaymentId = message.PaymentId,
                Reason = message.Reason
            }, context.CancellationToken);
        }
    }
}
