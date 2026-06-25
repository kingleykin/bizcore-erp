using Bizcore.BuildingBlocks.Audit;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Commands
{
    public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, bool>
    {
        private readonly CustomerDbContext _context;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<DeleteCustomerCommandHandler> _logger;

        public DeleteCustomerCommandHandler(
            CustomerDbContext context,
            IAuditPublisher audit,
            ILogger<DeleteCustomerCommandHandler> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (customer == null) throw new Exception("Customer not found");

            customer.MarkAsDeleted();
            await _context.SaveChangesAsync(cancellationToken);

            // Publish Audit Log
            await _audit.PublishAsync(
                AuditActions.Customer.BlockCustomer,
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                category: AuditCategory.Financial,
                classification: DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("CustomerDeleted CustomerId={CustomerId}", customer.Id);

            return true;
        }
    }
}
