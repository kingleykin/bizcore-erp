using Bizcore.BuildingBlocks.Abstractions;
using Customer.API.Application.DTOs;
using MediatR;

namespace Customer.API.Application.Commands.CustomerGroup;

public record CreateCustomerGroupCommand(
    string NameCustomerGroup,
    string Code,
    string Description
) : IRequest<CustomerGroupResponseDto>, ITransactionalCommand;
