using MediatR;
using Invoice.API.Domain.Entities;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;

namespace Invoice.API.Application.Commands
{
    public record UpdateInvoiceStatusCommand(
        Guid Id,
        InvoiceStatus Status,
        long Version
    ) : IRequest<bool>, ITransactionalCommand;
}
