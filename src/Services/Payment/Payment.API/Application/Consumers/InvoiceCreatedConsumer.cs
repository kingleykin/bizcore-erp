using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    public class InvoiceCreatedConsumer : IConsumer<IInvoiceCreatedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceCreatedConsumer> _logger;

        public InvoiceCreatedConsumer(AppDbContext context, ILogger<InvoiceCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
        {
            var message = context.Message;

            var exists = await _context.Invoices.AnyAsync(i => i.Id == message.Id);
            if (exists)
            {
                _logger.LogInformation("Invoice {InvoiceId} already exists in Payment read model. Skipping.", message.Id);
                return;
            }

            _context.Invoices.Add(new Invoice
            {
                Id = message.Id,
                Status = InvoiceStatus.Pending
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice {InvoiceId} synced to Payment read model.", message.Id);
        }
    }
}
