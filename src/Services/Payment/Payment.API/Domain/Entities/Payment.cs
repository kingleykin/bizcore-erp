using Bizcore.BuildingBlocks;

namespace Payment.API.Domain.Entities
{
    public enum PaymentStatus
    {
        Completed = 1,
        Reversed = 2
    }

    public class Payment
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    }

    public class Invoice
    {
        public Guid Id { get; set; }
        public InvoiceStatus Status { get; set; }
    }
}
