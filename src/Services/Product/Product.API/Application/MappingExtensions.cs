using Product.API.Application.DTOs;
using ProductEntity = Product.API.Domain.Entities.Product;

namespace Product.API.Application;

public static class MappingExtensions
{
    public static ProductResponseDto ToDto(this ProductEntity entity)
    {
        return new ProductResponseDto(
            Id: entity.Id,
            Code: entity.Code,
            Name: entity.Name,
            Unit: entity.Unit,
            Price: entity.Price,
            Description: entity.Description,
            IsActive: entity.IsActive,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }
}
