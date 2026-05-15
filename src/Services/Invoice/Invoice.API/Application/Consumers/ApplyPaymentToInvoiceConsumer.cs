using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;

namespace Invoice.API.Application.Consumers
{
    /// <summary>
    /// Xử lý Request-Reply từ Payment service.
    /// Validate + cập nhật invoice trong cùng 1 transaction,
    /// trả về kết quả đồng bộ để Payment biết ngay có thành công không.
    /// </summary>
    public class ApplyPaymentToInvoiceConsumer : IConsumer<IApplyPaymentToInvoiceRequest>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ApplyPaymentToInvoiceConsumer> _logger;

        public ApplyPaymentToInvoiceConsumer(AppDbContext context, ILogger<ApplyPaymentToInvoiceConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IApplyPaymentToInvoiceRequest> context)
        {
            var msg = context.Message;
            // Đọc từ header X-Correlation-ID do CorrelationIdSendFilter inject từ Payment service.
            // Không dùng context.CorrelationId vì đó là MassTransit envelope field, khác với
            // X-Correlation-ID header của hệ thống — và chỉ có giá trị khi sender set tường minh.
            var correlationId = context.Headers?.Get<string>("X-Correlation-ID") ?? "N/A";

            _logger.LogInformation(
                "[Invoice]: ApplyPayment request received CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId} Amount={Amount}",
                correlationId, msg.PaymentId, msg.InvoiceId, msg.Amount);

            var invoice = await _context.Invoices.FindAsync(msg.InvoiceId);
            var error = Validate(invoice, msg);

            if (error != null)
            {
                _logger.LogWarning(
                    "[Invoice]: ApplyPayment rejected CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId}: {Reason}",
                    correlationId, msg.PaymentId, msg.InvoiceId, error);

                await context.RespondAsync<IApplyPaymentToInvoiceResponse>(new
                {
                    Success = false,
                    ErrorReason = error
                });
                return;
            }

            invoice!.UpdateStatus(InvoiceStatus.Paid);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Invoice]: Invoice {InvoiceId} marked as Paid for PaymentId={PaymentId} CorrelationId={CorrelationId}",
                msg.InvoiceId, msg.PaymentId, correlationId);

            await context.RespondAsync<IApplyPaymentToInvoiceResponse>(new
            {
                Success = true,
                ErrorReason = (string?)null
            });
        }

        private static string? Validate(InvoiceEntity? invoice, IApplyPaymentToInvoiceRequest msg)
        {
            if (invoice == null)
                return "Invoice not found.";

            if (invoice.Status == InvoiceStatus.Cancelled)
                return "Invoice is cancelled.";

            if (invoice.Status == InvoiceStatus.Paid)
                return "Invoice is already paid.";

            if (invoice.Status != InvoiceStatus.Pending)
                return $"Invoice status '{invoice.Status}' is not valid for payment.";

            if (invoice.Amount != msg.Amount)
                return $"Amount mismatch: expected {invoice.Amount}, got {msg.Amount}.";

            return null;
        }
    }
}
