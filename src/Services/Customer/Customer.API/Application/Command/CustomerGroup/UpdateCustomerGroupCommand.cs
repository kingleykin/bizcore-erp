using Customer.API.Application.DTOs;
using Bizcore.BuildingBlocks;
using Customer.API.Domain.Entities;
using MediatR;

namespace Customer.API.Application.Commands.CustomerGroup;

public record UpdateCustomerGroupCommand(
    Guid Id,
    string NameCustomerGroup,
    string Description,
    CustomerGroupStatus Status
) : IRequest<CustomerGroupResponseDto>;
