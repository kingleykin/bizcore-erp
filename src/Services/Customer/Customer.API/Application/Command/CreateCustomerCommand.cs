using MediatR;
using Bizcore.BuildingBlocks.Abstractions;
using Customer.API.Application.DTOs;

namespace Customer.API.Application.Commands;

public record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address,
    Guid? CustomerGroupId = null
) : IRequest<CustomerResponseDto>, ITransactionalCommand;
