using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using Invoice.API.Application.DTOs;

namespace Invoice.API.Application;

public static class MappingExtensions
{
    public static InvoiceResponseDto ToDto(this InvoiceEntity entity)
    {
        return new InvoiceResponseDto(
            Id: entity.Id,
            OrderId: entity.OrderId,
            CustomerName: entity.CustomerName,
            Amount: entity.Amount,
            Status: entity.Status,
            CreatedAt: entity.CreatedAt
        );
    }
}
