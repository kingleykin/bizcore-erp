using MediatR;
using Customer.API.Application.DTOs;

namespace Customer.API.Application.Commands
{
    public record UpdateCustomerCommand(
        Guid Id,
        string FirstName,
        string LastName,
        string Phone,
        string Address,
        Guid? CustomerGroupId,
        long Version) : IRequest<CustomerResponseDto>;
}
