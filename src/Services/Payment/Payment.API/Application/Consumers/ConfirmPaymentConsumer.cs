using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IConfirmPaymentCommand từ Saga orchestrator
    /// sau khi Invoice service đã validate thành công.
    /// Cập nhật Payment.Status = Completed và publish IPaymentConfirmedEvent.
    /// </summary>
    public class ConfirmPaymentConsumer : IConsumer<IConfirmPaymentCommand>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ConfirmPaymentConsumer> _logger;

        public ConfirmPaymentConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<ConfirmPaymentConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IConfirmPaymentCommand> context)
        {
            var cmd = context.Message;
            var correlationId = context.Headers?.Get<string>("X-Correlation-ID") ?? "N/A";

            _logger.LogInformation(
                "[Payment] ConfirmPayment command received CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId}",
                correlationId, cmd.PaymentId, cmd.InvoiceId);

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == cmd.PaymentId);
            if (payment == null)
            {
                _logger.LogWarning(
                    "[Payment] Payment not found for confirmation CorrelationId={CorrelationId} PaymentId={PaymentId}",
                    correlationId, cmd.PaymentId);
                return;
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                _logger.LogInformation(
                    "[Payment] Payment already completed CorrelationId={CorrelationId} PaymentId={PaymentId}",
                    correlationId, cmd.PaymentId);
                return;
            }

            payment.Status = PaymentStatus.Completed;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Payment] Payment confirmed successfully CorrelationId={CorrelationId} PaymentId={PaymentId}",
                correlationId, cmd.PaymentId);

            // Publish event để Saga biết payment đã hoàn tất
            await _publishEndpoint.Publish<IPaymentConfirmedEvent>(new
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                ConfirmedAt = DateTime.UtcNow
            });

            // Publish legacy event để Report service cập nhật read model
            await _publishEndpoint.Publish<IPaymentCompletedEvent>(new
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate
            });
        }
    }
}
