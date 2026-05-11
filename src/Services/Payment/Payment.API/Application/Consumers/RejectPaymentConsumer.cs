using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Payment.API.Application.Hubs;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IRejectPaymentCommand từ Saga orchestrator
    /// sau khi Invoice service validation failed.
    /// Cập nhật Payment.Status = Failed và publish IPaymentRejectedEvent.
    /// </summary>
    public class RejectPaymentConsumer : IConsumer<IRejectPaymentCommand>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly ILogger<RejectPaymentConsumer> _logger;

        public RejectPaymentConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IHubContext<PaymentHub> hubContext,
            ILogger<RejectPaymentConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IRejectPaymentCommand> context)
        {
            var cmd = context.Message;
            var correlationId = context.Headers?.Get<string>("X-Correlation-ID") ?? "N/A";

            _logger.LogInformation(
                "[Payment] RejectPayment command received CorrelationId={CorrelationId} PaymentId={PaymentId} Reason={Reason}",
                correlationId, cmd.PaymentId, cmd.Reason);

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == cmd.PaymentId);
            if (payment == null)
            {
                _logger.LogWarning(
                    "[Payment] Payment not found for rejection CorrelationId={CorrelationId} PaymentId={PaymentId}",
                    correlationId, cmd.PaymentId);
                return;
            }

            if (payment.Status == PaymentStatus.Failed)
            {
                _logger.LogInformation(
                    "[Payment] Payment already failed CorrelationId={CorrelationId} PaymentId={PaymentId}",
                    correlationId, cmd.PaymentId);
                return;
            }

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = cmd.Reason;
            await _context.SaveChangesAsync();

            _logger.LogWarning(
                "[Payment] Payment rejected CorrelationId={CorrelationId} PaymentId={PaymentId} Reason={Reason}",
                correlationId, cmd.PaymentId, cmd.Reason);

            // Publish event để Saga biết payment đã bị reject
            await _publishEndpoint.Publish<IPaymentRejectedEvent>(new
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                Reason = cmd.Reason,
                RejectedAt = DateTime.UtcNow
            });

            // SignalR Push Notification (UX Enhancement Layer)
            await _hubContext.Clients.Group(payment.Id.ToString()).SendAsync("PaymentStatusUpdated", new 
            {
                PaymentId = payment.Id,
                Status = "Failed",
                FailureReason = cmd.Reason
            });
        }
    }
}
