using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Customer.API.Domain.Entities;

namespace Customer.API.Application.Commands.CustomerGroup;

public class UpdateCustomerGroupCommandHandler : IRequestHandler<UpdateCustomerGroupCommand, CustomerGroupResponseDto>
{
    private readonly CustomerDbContext _context;

    public UpdateCustomerGroupCommandHandler(CustomerDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerGroupResponseDto> Handle(UpdateCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var customerGroup = await _context.CustomerGroups.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (customerGroup == null) throw new Exception("Customer Group not found");

        customerGroup.UpdateNameCustomerGroup(request.NameCustomerGroup);
        customerGroup.UpdateDescription(request.Description);
        customerGroup.UpdateStatus(request.Status);

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
