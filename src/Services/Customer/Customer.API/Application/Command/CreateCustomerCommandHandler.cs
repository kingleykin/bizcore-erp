using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Application.DTOs;
using Customer.API.Domain.Entities;
using Customer.API.Infrastructure.Data;
using MassTransit;
using MediatR;

namespace Customer.API.Application.Commands
{
    public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerResponseDto>
    {
        private readonly CustomerDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<CreateCustomerCommandHandler> _logger;

        public CreateCustomerCommandHandler(
            CustomerDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<CreateCustomerCommandHandler> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task<CustomerResponseDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            var customer = Customers.Create(request.FirstName, request.LastName, request.Email, request.Phone, request.Address);

            _context.Customers.Add(customer);

            // Publish Event
            await _publishEndpoint.Publish<ICustomerCreatedEvent>(new
            {
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.Status,
                customer.CreatedAt
            }, cancellationToken);

            // Publish Audit Log
            await _audit.PublishAsync(
                AuditActions.Customer.Created,
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                after: new { customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.Phone, customer.Address, customer.Status },
                category: AuditCategory.Financial,
                classification: DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("CustomerCreated CustomerId={CustomerId}", customer.Id);

            return new CustomerResponseDto(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.Status,
                customer.CustomerPoint,
                customer.CreatedAt
            );
        }
    }
}
