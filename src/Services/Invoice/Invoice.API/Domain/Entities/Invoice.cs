using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Invoice.API.Domain.Entities
{
    /// <summary>
    /// Invoice entity.
    /// </summary>
    public class Invoice : BaseEntity
    {
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

        // ── Factory ───────────────────────────────────────────────────────────

        public static Invoice Create(string customerName, decimal amount)
        {
            if (amount > 1_000_000_000)
                throw new DomainException("Hóa đơn không được vượt quá hạn mức 1,000,000 VNĐ.");

            return new Invoice
            {
                CustomerName = customerName,
                Amount = amount,
                Status = InvoiceStatus.Pending
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

