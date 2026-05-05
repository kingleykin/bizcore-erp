using Bizcore.BuildingBlocks.Exceptions;

namespace Invoice.API.Domain.Entities
{
    public enum InvoiceStatus
    {
        Pending = 0,
        Paid = 1,
        Cancelled = 2
    }

    public class Invoice
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Domain Validation (Business Rules)
        public static Invoice Create(string customerName, decimal amount)
        {
            if (amount > 1000000) // Business Rule: Limit 1,000,000 for demo
            {
                throw new DomainException("Hóa đơn không được vượt quá hạn mức 1,000,000 VNĐ.");
            }

            return new Invoice
            {
                Id = Guid.NewGuid(),
                CustomerName = customerName,
                Amount = amount,
                Status = InvoiceStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
