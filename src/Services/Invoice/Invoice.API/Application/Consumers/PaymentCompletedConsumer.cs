using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Application.Consumers
{
    /// <summary>
    /// Consumer lắng nghe IPaymentCompletedEvent từ Payment service.
    /// Đây là bước cuối cùng của luồng thanh toán để chính thức đánh dấu hóa đơn là đã thanh toán.
    /// </summary>
    public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentCompletedConsumer> _logger;

        public PaymentCompletedConsumer(AppDbContext context, ILogger<PaymentCompletedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
        {
            var msg = context.Message;
            var correlationId = context.Headers?.Get<string>("X-Correlation-ID") ?? "N/A";

            // IPaymentCompletedEvent dùng chung cho cả Invoice lẫn Order — bỏ qua nếu đây là
            // thanh toán cho Order (không liên quan Invoice.API).
            if (msg.InvoiceId is not { } invoiceId)
                return;

            _logger.LogInformation(
                "[Invoice] PaymentCompleted event received CorrelationId={CorrelationId} PaymentId={PaymentId} InvoiceId={InvoiceId}",
                correlationId, msg.PaymentId, invoiceId);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
            {
                _logger.LogWarning(
                    "[Invoice] Invoice not found for payment completion CorrelationId={CorrelationId} InvoiceId={InvoiceId}",
                    correlationId, msg.InvoiceId);
                return;
            }

            if (invoice.Status == InvoiceStatus.Paid)
            {
                _logger.LogInformation(
                    "[Invoice] Invoice already marked as Paid CorrelationId={CorrelationId} InvoiceId={InvoiceId}",
                    correlationId, msg.InvoiceId);
                return;
            }

            invoice.UpdateStatus(InvoiceStatus.Paid);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Invoice] Invoice {InvoiceId} successfully marked as Paid. CorrelationId={CorrelationId}",
                msg.InvoiceId, correlationId);
        }
    }
}
