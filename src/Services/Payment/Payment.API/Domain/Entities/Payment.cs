using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;

namespace Payment.API.Domain.Entities
{
    public enum PaymentStatus
    {
        /// <summary>Đang chờ Saga orchestrator validate invoice.</summary>
        Processing = 0,
        /// <summary>Saga đã confirm: invoice hợp lệ, payment hoàn tất.</summary>
        Completed = 1,
        /// <summary>Compensation: payment bị đảo ngược sau khi đã Completed.</summary>
        Reversed = 2,
        /// <summary>Saga đã reject: invoice validation failed, payment không được commit.</summary>
        Failed = 3
    }

    public class Payment : AggregateRoot
    {
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public PaymentStatus Status { get; set; } = PaymentStatus.Processing;
        public string? IdempotencyKey { get; set; }
        public string? FailureReason { get; set; }
    }

    public class Invoice : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public InvoiceStatus Status { get; set; }
    }
}
