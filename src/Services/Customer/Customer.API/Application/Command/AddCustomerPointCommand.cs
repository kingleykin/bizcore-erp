using MediatR;
using Bizcore.BuildingBlocks.Abstractions;

namespace Customer.API.Application.Commands;

public record AddCustomerPointCommand(
    Guid PaymentId,
    Guid CustomerId,
    decimal Amount
) : IRequest<bool>;
