using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks;
using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Application.Consumers
{
    public class RevertInvoicePaymentConsumer : IConsumer<IRevertInvoicePaymentCommand>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RevertInvoicePaymentConsumer> _logger;

        public RevertInvoicePaymentConsumer(AppDbContext context, ILogger<RevertInvoicePaymentConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IRevertInvoicePaymentCommand> context)
        {
            var message = context.Message;
            _logger.LogInformation("Processing RevertInvoicePaymentCommand for InvoiceId={InvoiceId}, Reason={Reason}", message.InvoiceId, message.Reason);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == message.InvoiceId, context.CancellationToken);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found to revert.", message.InvoiceId);
                return;
            }

            // Revert status to Pending (or whatever status you consider appropriate before payment)
            invoice.UpdateStatus(InvoiceStatus.Pending);
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("Invoice {InvoiceId} status reverted successfully.", message.InvoiceId);

            // Publish confirmation event back to Saga
            await context.Publish<IInvoicePaymentRevertedEvent>(new
            {
                PaymentId = message.PaymentId,
                InvoiceId = message.InvoiceId,
                Reason = message.Reason
            }, context.CancellationToken);
        }
    }
}
