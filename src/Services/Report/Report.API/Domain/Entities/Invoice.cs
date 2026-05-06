using Bizcore.BuildingBlocks;

namespace Report.API.Domain.Entities
{
    public class Invoice
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public InvoiceStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
