using Invoice.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.Commands
{
    public class UpdateInvoiceStatusCommandHandler : IRequestHandler<UpdateInvoiceStatusCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger;

        public UpdateInvoiceStatusCommandHandler(AppDbContext context, ILogger<UpdateInvoiceStatusCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateInvoiceStatusCommand request, CancellationToken cancellationToken)
        {
            var invoice = await _context.Invoices.FindAsync(new object[] { request.Id }, cancellationToken);
            if (invoice is null) return false;

            invoice.UpdateStatus(request.Status);

            // Set the original version for concurrency check to detect if another user changed it
            _context.Entry(invoice).Property(x => x.Version).OriginalValue = request.Version;

            // Do NOT call SaveChangesAsync here.
            // TransactionBehavior will call it via UnitOfWork.CommitAsync.

            _logger.LogInformation("Invoice status updated. InvoiceId: {InvoiceId}, Status: {Status}", request.Id, request.Status);

            return true;
        }
    }
}
