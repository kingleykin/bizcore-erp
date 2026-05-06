using Bizcore.BuildingBlocks.Contracts;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<PaymentCompletedConsumer> _logger;

        public PaymentCompletedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<PaymentCompletedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation(
                "Processing PaymentCompletedEvent PaymentId={PaymentId} InvoiceId={InvoiceId}",
                message.PaymentId,
                message.InvoiceId);

            var invoice = await _context.Invoices.FindAsync(message.InvoiceId);

            var validationError = ValidateInvoiceForPayment(invoice, message);
            if (validationError != null)
            {
                _logger.LogWarning(
                    "Payment application failed for InvoiceId={InvoiceId} PaymentId={PaymentId}: {Reason}",
                    message.InvoiceId,
                    message.PaymentId,
                    validationError);
                await PublishCompensationAsync(message, validationError);
                return;
            }

            invoice!.Status = InvoiceStatus.Paid;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Invoice {InvoiceId} updated to PAID", message.InvoiceId);
        }

        /// <summary>
        /// Returns a human-readable failure reason when invoice must not accept this payment
        /// (triggers business rollback / Reversed on Payment). Null means validation passed.
        /// </summary>
        private static string? ValidateInvoiceForPayment(InvoiceEntity? invoice, IPaymentCompletedEvent message)
        {
            if (invoice == null)
            {
                return "Invoice not found while applying payment completion event.";
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                return "Invoice is cancelled; payment cannot be applied.";
            }

            if (invoice.Status == InvoiceStatus.Paid)
            {
                return "Invoice is already paid; duplicate payment cannot be applied.";
            }

            if (invoice.Status != InvoiceStatus.Pending)
            {
                return $"Invoice status {invoice.Status} is not valid for payment application.";
            }

            if (invoice.Amount != message.Amount)
            {
                return $"Payment amount {message.Amount} does not match invoice amount {invoice.Amount}.";
            }

            return null;
        }

        private async Task PublishCompensationAsync(IPaymentCompletedEvent message, string reason)
        {
            await _publishEndpoint.Publish<IPaymentCompensationRequestedEvent>(new
            {
                PaymentId = message.PaymentId,
                InvoiceId = message.InvoiceId,
                Amount = message.Amount,
                RequestedAt = DateTime.UtcNow,
                Reason = reason
            });
        }
    }
}
