using MediatR;
using Bizcore.BuildingBlocks.Abstractions;
using Invoice.API.Application.DTOs;

namespace Invoice.API.Application.Commands;

public record CreateInvoiceCommand(
    Guid CustomerId,
    string CustomerName,
    decimal Amount
) : IRequest<InvoiceResponseDto>, ITransactionalCommand;
