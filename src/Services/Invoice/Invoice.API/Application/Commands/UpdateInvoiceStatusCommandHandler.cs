using Invoice.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.Commands
{
    public class UpdateInvoiceStatusCommandHandler : IRequestHandler<UpdateInvoiceStatusCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly Bizcore.BuildingBlocks.Audit.IAuditPublisher _audit;
        private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger;

        public UpdateInvoiceStatusCommandHandler(AppDbContext context, Bizcore.BuildingBlocks.Audit.IAuditPublisher audit, ILogger<UpdateInvoiceStatusCommandHandler> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateInvoiceStatusCommand request, CancellationToken cancellationToken)
        {
            var invoice = await _context.Invoices.FindAsync(new object[] { request.Id }, cancellationToken);
            if (invoice is null) return false;

            var beforeState = new { invoice.Status };

            invoice.UpdateStatus(request.Status);

            // Set the original version for concurrency check to detect if another user changed it
            _context.Entry(invoice).Property(x => x.Version).OriginalValue = request.Version;

            var afterState = new { invoice.Status };

            await _audit.PublishAsync(
                "InvoiceStatusUpdated",
                entityType: "Invoice",
                entityId: invoice.Id.ToString(),
                before: beforeState,
                after: afterState,
                category: Bizcore.BuildingBlocks.Audit.AuditCategory.Financial,
                classification: Bizcore.BuildingBlocks.Audit.DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("Invoice status updated. InvoiceId: {InvoiceId}, Status: {Status}", request.Id, request.Status);

            return true;
        }
    }
}
