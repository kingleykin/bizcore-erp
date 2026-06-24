using Customer.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Customer.API.Application.DTOs;

public record CustomerResponseDto
(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address,
    CustomerStatus Status,
    int CustomerPoint,
    DateTime CreatedAt
);
