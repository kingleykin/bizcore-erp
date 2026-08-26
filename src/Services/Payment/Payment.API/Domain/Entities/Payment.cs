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
        /// <summary>Đúng một trong InvoiceId/OrderId được set — payment trả cho hóa đơn (luồng cũ)
        /// hoặc trả cho đơn hàng (luồng Order), không bao giờ cả hai.</summary>
        public Guid? InvoiceId { get; set; }
        public Guid? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public PaymentStatus Status { get; set; } = PaymentStatus.Processing;
        public string? IdempotencyKey { get; set; }
        public string? FailureReason { get; set; }
    }

    public class Invoice : BaseEntity
    {
        public InvoiceStatus Status { get; set; }
    }

    /// <summary>
    /// Read-model tối giản mirror Order bên Order.API (đồng bộ qua OrderCreatedEvent), chỉ dùng
    /// để kiểm tra tồn tại nhanh (fail-fast) tại bước Initiate — validate đầy đủ (trạng thái, số
    /// tiền) do ValidateOrderCommandConsumer bên Order.API đảm nhiệm qua saga.
    /// </summary>
    public class Order : BaseEntity
    {
    }
}
