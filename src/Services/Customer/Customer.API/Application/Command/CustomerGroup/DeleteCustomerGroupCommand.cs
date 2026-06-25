using MediatR;
using Bizcore.BuildingBlocks;

namespace Customer.API.Application.Commands.CustomerGroup;

public record DeleteCustomerGroupCommand(Guid Id) : IRequest<bool>;
