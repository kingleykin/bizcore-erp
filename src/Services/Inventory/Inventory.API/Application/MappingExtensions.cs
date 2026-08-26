using Inventory.API.Application.DTOs;
using StockEntity = Inventory.API.Domain.Entities.Stock;
using StockTransactionEntity = Inventory.API.Domain.Entities.StockTransaction;

namespace Inventory.API.Application;

public static class MappingExtensions
{
    public static StockResponseDto ToDto(this StockEntity entity)
    {
        return new StockResponseDto(
            Id: entity.Id,
            ProductId: entity.ProductId,
            ProductName: entity.ProductName,
            QuantityOnHand: entity.QuantityOnHand,
            QuantityReserved: entity.QuantityReserved,
            AvailableQuantity: entity.AvailableQuantity,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }

    public static StockTransactionDto ToDto(this StockTransactionEntity entity)
    {
        return new StockTransactionDto(
            Id: entity.Id,
            ProductId: entity.ProductId,
            ProductName: entity.ProductName,
            Type: entity.Type.ToString(),
            Quantity: entity.Quantity,
            QuantityOnHandAfter: entity.QuantityOnHandAfter,
            QuantityReservedAfter: entity.QuantityReservedAfter,
            RelatedOrderId: entity.RelatedOrderId,
            Note: entity.Note,
            CreatedAt: entity.CreatedAt
        );
    }
}
