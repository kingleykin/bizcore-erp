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

            _logger.LogInformation(
                "Updating Report read model for PaymentId={PaymentId} InvoiceId={InvoiceId}",
                msg.PaymentId, msg.InvoiceId);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == msg.InvoiceId);
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
