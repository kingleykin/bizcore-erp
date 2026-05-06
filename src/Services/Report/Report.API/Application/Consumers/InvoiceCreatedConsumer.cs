using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Report.API.Domain.Entities;
using Report.API.Infrastructure.Data;

namespace Report.API.Application.Consumers
{
    public class InvoiceCreatedConsumer : IConsumer<IInvoiceCreatedEvent>
    {
        private readonly AppDbContext _context;

        public InvoiceCreatedConsumer(AppDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
        {
            var message = context.Message;
            
            var invoice = new Invoice
            {
                Id = message.Id,
                CustomerName = message.CustomerName,
                Amount = message.Amount,
                Status = Bizcore.BuildingBlocks.InvoiceStatus.Pending,
                CreatedAt = message.CreatedAt
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
        }
    }
}
