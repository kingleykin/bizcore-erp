using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using CustomerGroupEntity = Customer.API.Domain.Entities.CustomerGroup;

namespace Customer.API.Application.Commands.CustomerGroup;

public class CreateCustomerGroupCommandHandler : IRequestHandler<CreateCustomerGroupCommand, CustomerGroupResponseDto>
{
    private readonly CustomerDbContext _context;

    public CreateCustomerGroupCommandHandler(CustomerDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerGroupResponseDto> Handle(CreateCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var customerGroup = CustomerGroupEntity.Create(request.NameCustomerGroup, request.Code, request.Description);

        _context.CustomerGroups.Add(customerGroup);

        await _context.SaveChangesAsync(cancellationToken);

        return new CustomerGroupResponseDto(
            customerGroup.Id,
            customerGroup.NameCustomerGroup,
            customerGroup.Code,
            customerGroup.Description,
            customerGroup.Status,
            customerGroup.CreatedAt
        );
    }
}
