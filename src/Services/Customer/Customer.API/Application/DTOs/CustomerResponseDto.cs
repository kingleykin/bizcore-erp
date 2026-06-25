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
    int SoTienTrongTaiKhoan,
    int SoTienTongHoaDon,
    Guid? CustomerGroupId,
    DateTime CreatedAt,
    long Version
);
