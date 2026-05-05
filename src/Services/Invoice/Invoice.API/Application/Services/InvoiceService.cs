using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

        public InvoiceService(AppDbContext context)
        {
            _context = context;
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
            invoice.Id = Guid.NewGuid();
            invoice.CreatedAt = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Pending;

            _context.Invoices.Add(invoice);
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
