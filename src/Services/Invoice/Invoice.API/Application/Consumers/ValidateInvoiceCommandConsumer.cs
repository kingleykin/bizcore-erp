using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;

namespace Invoice.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IValidateInvoiceCommand từ Saga orchestrator.
    /// Validate invoice và cập nhật trạng thái Paid nếu hợp lệ,
    /// sau đó publish IInvoiceValidatedEvent hoặc IInvoiceValidationFailedEvent.
    /// </summary>
    public class ValidateInvoiceCommandConsumer : IConsumer<IValidateInvoiceCommand>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ValidateInvoiceCommandConsumer> _logger;

        public ValidateInvoiceCommandConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<ValidateInvoiceCommandConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IValidateInvoiceCommand> context)
        {
            var cmd = context.Message;
            var correlationId = context.Headers?.Get<string>("X-Correlation-ID") ?? "N/A";

            _logger.LogInformation(
                "[Invoice] ValidateInvoice command received CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId} Amount={Amount}",
                correlationId, cmd.PaymentId, cmd.InvoiceId, cmd.Amount);

            var invoice = await _context.Invoices.FindAsync(cmd.InvoiceId);
            var error = Validate(invoice, cmd);

            if (error != null)
            {
                _logger.LogWarning(
                    "[Invoice] Invoice validation failed CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId}: {Reason}",
                    correlationId, cmd.PaymentId, cmd.InvoiceId, error);

                // Publish validation failed event để Saga reject payment
                await _publishEndpoint.Publish<IInvoiceValidationFailedEvent>(new
                {
                    PaymentId = cmd.PaymentId,
                    InvoiceId = cmd.InvoiceId,
                    Reason = error,
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            // Validation thành công → cập nhật invoice status
            invoice!.Status = InvoiceStatus.Paid;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Invoice] Invoice validated and marked as Paid CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId}",
                correlationId, cmd.PaymentId, cmd.InvoiceId);

            // Publish validation success event để Saga confirm payment
            await _publishEndpoint.Publish<IInvoiceValidatedEvent>(new
            {
                PaymentId = cmd.PaymentId,
                InvoiceId = cmd.InvoiceId,
                ValidatedAt = DateTime.UtcNow
            });
        }

        private static string? Validate(InvoiceEntity? invoice, IValidateInvoiceCommand cmd)
        {
            if (invoice == null)
                return "Invoice not found.";

            if (invoice.Status == InvoiceStatus.Cancelled)
                return "Invoice is cancelled.";

            if (invoice.Status == InvoiceStatus.Paid)
                return "Invoice is already paid.";

            if (invoice.Status != InvoiceStatus.Pending)
                return $"Invoice status '{invoice.Status}' is not valid for payment.";

            if (invoice.Amount != cmd.Amount)
                return $"Amount mismatch: expected {invoice.Amount}, got {cmd.Amount}.";

            return null;
        }
    }
}
