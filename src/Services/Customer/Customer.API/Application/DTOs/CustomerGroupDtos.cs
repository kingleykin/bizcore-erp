namespace Customer.API.Application.DTOs;

public record CustomerGroupResponseDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCustomerGroupRequest(
    string Code,
    string Name,
    string? Description
);

public record UpdateCustomerGroupRequest(
    string Name,
    string? Description
);
