using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Report.API.Infrastructure.Data;

namespace Report.API.Application.Consumers
{
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

            // IPaymentCompletedEvent dùng chung cho cả Invoice lẫn Order — bỏ qua nếu đây là
            // thanh toán cho Order (Report read model hiện chỉ theo dõi Invoice).
            if (msg.InvoiceId is not { } invoiceId)
                return;

            _logger.LogInformation(
                "Updating Report read model for PaymentId={PaymentId} InvoiceId={InvoiceId}",
                msg.PaymentId, invoiceId);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId);
            if (invoice == null)
            {
                _logger.LogWarning(
                    "Invoice not found in Report read model InvoiceId={InvoiceId}, skipping",
                    msg.InvoiceId);
                return;
            }

            invoice.Status = Bizcore.BuildingBlocks.InvoiceStatus.Paid;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Report read model updated: Invoice {InvoiceId} marked as Paid", msg.InvoiceId);
        }
    }
}
