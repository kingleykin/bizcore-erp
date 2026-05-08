using MediatR;
using Invoice.API.Domain.Entities;

namespace Invoice.API.Application.Commands
{
    public record CreateInvoiceCommand(
        string CustomerName,
        decimal Amount
    ) : IRequest<Invoice.API.Domain.Entities.Invoice>;
}
