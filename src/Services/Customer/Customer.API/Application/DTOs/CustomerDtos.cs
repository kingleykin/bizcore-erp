namespace Customer.API.Application.DTOs;

public record CustomerResponseDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxCode,
    string? Email,
    string? Phone,
    string? Address,
    Guid CustomerGroupId,
    string CustomerGroupName,
    bool IsActive,
    int Points,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCustomerRequest(
    string Code,
    string Name,
    Guid CustomerGroupId,
    string? TaxCode,
    string? Email,
    string? Phone,
    string? Address
);

public record UpdateCustomerRequest(
    string Name,
    string? TaxCode,
    string? Email,
    string? Phone,
    string? Address
);

public record ChangeCustomerGroupRequest(
    Guid CustomerGroupId
);
