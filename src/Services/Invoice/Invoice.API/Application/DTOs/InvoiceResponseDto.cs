using Invoice.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.DTOs;

public record InvoiceResponseDto
(
    Guid Id,
    string CustomerName,
    decimal Amount,
    InvoiceStatus Status,
    DateTime CreatedAt
);
