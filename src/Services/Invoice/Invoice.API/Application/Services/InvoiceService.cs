using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;

namespace Invoice.API.Application.Services
{
    public interface IInvoiceService
    {
        Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync();
        Task<Invoice.API.Domain.Entities.Invoice?> GetByIdAsync(Guid id);
        Task<Invoice.API.Domain.Entities.Invoice> CreateAsync(Invoice.API.Domain.Entities.Invoice invoice);
        Task<bool> UpdateStatusAsync(Guid id, InvoiceStatus status);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public InvoiceService(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync()
        {
            return await _context.Invoices.ToListAsync();
        }

        public async Task<Invoice.API.Domain.Entities.Invoice?> GetByIdAsync(Guid id)
        {
            return await _context.Invoices.FindAsync(id);
        }

        public async Task<Invoice.API.Domain.Entities.Invoice> CreateAsync(Invoice.API.Domain.Entities.Invoice invoice)
        {
            _context.Invoices.Add(invoice);

            // Publish event (MassTransit Outbox will handle this)
            await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
            {
                Id = invoice.Id,
                CustomerName = invoice.CustomerName,
                Amount = invoice.Amount,
                CreatedAt = invoice.CreatedAt
            });

            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<bool> UpdateStatusAsync(Guid id, InvoiceStatus status)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return false;

            invoice.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
