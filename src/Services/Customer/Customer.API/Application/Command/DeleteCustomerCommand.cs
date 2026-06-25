using MediatR;

namespace Customer.API.Application.Commands
{
    public record DeleteCustomerCommand(Guid Id) : IRequest<bool>;
}
