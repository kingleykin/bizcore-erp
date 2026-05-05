using Bizcore.BuildingBlocks;

namespace Payment.API.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }

    public class Invoice
    {
        public Guid Id { get; set; }
        public InvoiceStatus Status { get; set; }
    }
}
