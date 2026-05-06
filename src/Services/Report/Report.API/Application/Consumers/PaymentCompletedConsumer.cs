using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Report.API.Infrastructure.Data;

namespace Report.API.Application.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
    {
        private readonly AppDbContext _context;

        public PaymentCompletedConsumer(AppDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == context.Message.InvoiceId);
            if (invoice != null)
            {
                invoice.Status = Bizcore.BuildingBlocks.InvoiceStatus.Paid;
                await _context.SaveChangesAsync();
            }
        }
    }
}
