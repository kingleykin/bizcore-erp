using Order.API.Application.DTOs;
using OrderEntity = Order.API.Domain.Entities.Order;
using OrderItemEntity = Order.API.Domain.Entities.OrderItem;

namespace Order.API.Application;

public static class MappingExtensions
{
    public static OrderItemDto ToDto(this OrderItemEntity entity)
    {
        return new OrderItemDto(
            Id: entity.Id,
            ProductId: entity.ProductId,
            ProductName: entity.ProductName,
            Quantity: entity.Quantity,
            UnitPrice: entity.UnitPrice,
            LineTotal: entity.LineTotal
        );
    }

    public static OrderResponseDto ToDto(this OrderEntity entity)
    {
        return new OrderResponseDto(
            Id: entity.Id,
            OrderNumber: entity.OrderNumber,
            CustomerId: entity.CustomerId,
            CustomerName: entity.CustomerName,
            OrderDate: entity.OrderDate,
            Note: entity.Note,
            TotalAmount: entity.TotalAmount,
            Status: entity.Status,
            CancelReason: entity.CancelReason,
            Items: entity.Items.Select(i => i.ToDto()).ToList(),
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }
}
