using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;

namespace Report.API.Domain.Entities
{
    public class Invoice : BaseEntity
    {
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public InvoiceStatus Status { get; set; }
    }
}
