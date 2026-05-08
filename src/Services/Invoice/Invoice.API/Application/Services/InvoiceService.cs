using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using Invoice.API.Application.Clients;
using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace Invoice.API.Application.Services
{
    public interface IInvoiceService
    {
        Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync();
        Task<Invoice.API.Domain.Entities.Invoice?>             GetByIdAsync(Guid id);
    }

    public record RestoreFieldResult(bool Success, string Message, Guid? NewAuditEntryId = null);

    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext     _context;

        public InvoiceService(AppDbContext context)
        {
            _context         = context;
        }

        public async Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync()
            => await _context.Invoices.ToListAsync();

        public async Task<Invoice.API.Domain.Entities.Invoice?> GetByIdAsync(Guid id)
            => await _context.Invoices.FindAsync(id);
    }
}

