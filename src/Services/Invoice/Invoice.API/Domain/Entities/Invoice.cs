using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Exceptions;

namespace Invoice.API.Domain.Entities
{
    /// <summary>
    /// Invoice entity.
    /// </summary>
    public class Invoice
    {
        public Guid          Id           { get; set; }
        public string        CustomerName { get; set; } = string.Empty;
        public decimal       Amount       { get; set; }
        public InvoiceStatus Status       { get; set; } = InvoiceStatus.Pending;
        public DateTime      CreatedAt    { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Concurrency token — EF Core dùng để phát hiện concurrent writes.
        /// Bắt buộc check trong mọi reversal operation để tránh Stale Snapshot Overwrite.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }

        // ── Factory ───────────────────────────────────────────────────────────

        public static Invoice Create(string customerName, decimal amount)
        {
            if (amount > 1_000_000)
                throw new DomainException("Hóa đơn không được vượt quá hạn mức 1,000,000 VNĐ.");

            return new Invoice
            {
                Id           = Guid.NewGuid(),
                CustomerName = customerName,
                Amount       = amount,
                Status       = InvoiceStatus.Pending,
                CreatedAt    = DateTime.UtcNow
            };
        }

        // ── Domain Reversal Methods (chỉ non-financial fields) ────────────────

        /// <summary>
        /// Khôi phục CustomerName về giá trị trước đó.
        /// Guard: không cho phép sửa Invoice đã Cancelled.
        /// </summary>
        public void RestoreCustomerName(string previousName)
        {
            if (Status == InvoiceStatus.Cancelled)
                throw new DomainException("Không thể khôi phục Invoice đã bị hủy.");

            if (string.IsNullOrWhiteSpace(previousName))
                throw new DomainException("Tên khách hàng không được để trống.");

            CustomerName = previousName;
        }
    }
}

