namespace Product.API.Application.DTOs;

public record ProductResponseDto(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    decimal Price,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateProductRequest(
    string Name,
    string Unit,
    decimal Price,
    string? Description
);

public record UpdateProductRequest(
    string Name,
    string Unit,
    decimal Price,
    string? Description
);
