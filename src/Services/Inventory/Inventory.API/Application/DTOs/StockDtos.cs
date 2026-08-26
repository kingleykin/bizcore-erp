namespace Inventory.API.Application.DTOs;

public record StockResponseDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdjustStockRequest(
    string ProductName,
    int QuantityOnHand
);

public record StockTransactionDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Type,
    int Quantity,
    int QuantityOnHandAfter,
    int QuantityReservedAfter,
    Guid? RelatedOrderId,
    string? Note,
    DateTime CreatedAt
);
