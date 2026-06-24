using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Application.DTOs;
using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using MediatR;

namespace Invoice.API.Application.Commands
{
    public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceResponseDto>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<CreateInvoiceCommandHandler> _logger;

        public CreateInvoiceCommandHandler(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<CreateInvoiceCommandHandler> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task<InvoiceResponseDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var invoice = Domain.Entities.Invoice.Create(request.CustomerId, request.CustomerName, request.Amount);

            _context.Invoices.Add(invoice);

            // Publish Event
            await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
            {
                invoice.Id,
                invoice.CustomerId,
                invoice.CustomerName,
                invoice.Amount,
                invoice.CreatedAt
            }, cancellationToken);

            // Publish Audit Log
            await _audit.PublishAsync(
                AuditActions.Invoice.Created,
                entityType: "Invoice",
                entityId: invoice.Id.ToString(),
                after: new { invoice.Id, invoice.CustomerName, invoice.Amount, invoice.Status },
                category: AuditCategory.Financial,
                classification: DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("InvoiceCreated InvoiceId={InvoiceId}", invoice.Id);

            return invoice.ToDto();
        }
    }
}
