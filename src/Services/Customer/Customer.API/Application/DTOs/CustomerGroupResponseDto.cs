using Customer.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Customer.API.Application.DTOs;

public record CustomerGroupResponseDto
(
    Guid Id,
    string NameCustomerGroup,
    string Code,
    string Description,
    CustomerGroupStatus Status,
    DateTime CreatedAt
);
