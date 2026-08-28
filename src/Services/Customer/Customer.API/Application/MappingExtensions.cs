using Customer.API.Application.DTOs;
using CustomerEntity = Customer.API.Domain.Entities.Customer;
using CustomerGroupEntity = Customer.API.Domain.Entities.CustomerGroup;

namespace Customer.API.Application;

public static class MappingExtensions
{
    public static CustomerResponseDto ToDto(this CustomerEntity entity)
    {
        return new CustomerResponseDto(
            Id: entity.Id,
            Code: entity.Code,
            Name: entity.Name,
            TaxCode: entity.TaxCode,
            Email: entity.Email,
            Phone: entity.Phone,
            Address: entity.Address,
            CustomerGroupId: entity.CustomerGroupId,
            CustomerGroupName: entity.CustomerGroup?.Name ?? string.Empty,
            IsActive: entity.IsActive,
            Points: entity.Points,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }

    public static CustomerGroupResponseDto ToDto(this CustomerGroupEntity entity)
    {
        return new CustomerGroupResponseDto(
            Id: entity.Id,
            Code: entity.Code,
            Name: entity.Name,
            Description: entity.Description,
            IsActive: entity.IsActive,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }
}
