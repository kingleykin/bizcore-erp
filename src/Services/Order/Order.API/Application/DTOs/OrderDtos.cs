using Bizcore.BuildingBlocks;

namespace Order.API.Application.DTOs;

public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public record OrderResponseDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    DateTime OrderDate,
    string? Note,
    decimal TotalAmount,
    OrderStatus Status,
    string? CancelReason,
    IReadOnlyCollection<OrderItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);

public record CreateOrderRequest(
    Guid CustomerId,
    string? Note,
    List<CreateOrderItemRequest> Items
);

public record CancelOrderRequest(
    string Reason
);
