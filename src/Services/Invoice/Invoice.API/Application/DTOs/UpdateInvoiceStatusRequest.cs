using Invoice.API.Domain.Entities;
using Bizcore.BuildingBlocks;

namespace Invoice.API.Application.DTOs
{
    public record UpdateInvoiceStatusRequest(
        InvoiceStatus Status,
        long Version
    );
}
