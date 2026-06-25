using Bizcore.BuildingBlocks.Audit;
using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Commands
{
    public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, CustomerResponseDto>
    {
        private readonly CustomerDbContext _context;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<UpdateCustomerCommandHandler> _logger;

        public UpdateCustomerCommandHandler(
            CustomerDbContext context,
            IAuditPublisher audit,
            ILogger<UpdateCustomerCommandHandler> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task<CustomerResponseDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (customer == null) throw new Exception("Customer not found");

            var beforeState = new { customer.Id, customer.FirstName, customer.LastName, customer.Phone, customer.Address, customer.CustomerGroupId };

            customer.Update(request.FirstName, request.LastName, request.Phone, request.Address, request.CustomerGroupId);

            await _context.SaveChangesAsync(cancellationToken);

            var afterState = new { customer.Id, customer.FirstName, customer.LastName, customer.Phone, customer.Address, customer.CustomerGroupId };

            // Publish Audit Log
            await _audit.PublishAsync(
                AuditActions.Customer.Updated,
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                before: beforeState,
                after: afterState,
                category: AuditCategory.Financial,
                classification: DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("CustomerUpdated CustomerId={CustomerId}", customer.Id);

            return new CustomerResponseDto(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.Status,
                customer.CustomerPoint,
                customer.SoTienTrongTaiKhoan,
                customer.SoTienTongHoaDon,
                customer.CustomerGroupId,
                customer.CreatedAt
            );
        }
    }
}
