using MediatR;
using Invoice.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.Commands
{
    public record UpdateInvoiceStatusCommand(
        Guid Id,
        InvoiceStatus Status
    ) : IRequest<bool>;
}
