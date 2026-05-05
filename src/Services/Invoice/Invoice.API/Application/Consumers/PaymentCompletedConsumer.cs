using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using MassTransit;

namespace Invoice.API.Application.Consumers
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
            _logger.LogInformation("Processing PaymentCompletedEvent for Invoice: {InvoiceId}", context.Message.InvoiceId);

            var invoice = await _context.Invoices.FindAsync(context.Message.InvoiceId);
            if (invoice != null)
            {
                invoice.Status = InvoiceStatus.Paid;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to PAID", context.Message.InvoiceId);
            }
            else
            {
                _logger.LogWarning("Invoice {InvoiceId} not found", context.Message.InvoiceId);
            }
        }
    }
}
