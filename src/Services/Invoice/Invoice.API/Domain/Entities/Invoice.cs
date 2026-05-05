using System.ComponentModel.DataAnnotations;

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
        
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        
        public decimal Amount { get; set; }
        
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
