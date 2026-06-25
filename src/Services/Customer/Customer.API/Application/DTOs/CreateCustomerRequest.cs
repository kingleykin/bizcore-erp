using Customer.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Customer.API.Application.DTOs;

public record CreateCustomerRequest
(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address,
    Guid? CustomerGroupId = null
);
