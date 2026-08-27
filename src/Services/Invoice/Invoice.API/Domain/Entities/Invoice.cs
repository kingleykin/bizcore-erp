using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using System.Collections.Generic;

namespace Invoice.API.Domain.Entities
{
    /// <summary>
    /// Invoice aggregate root.
    /// Encapsulates state and enforces domain rules.
    /// </summary>
    public class Invoice : AggregateRoot
    {
        /// <summary>Đơn hàng gốc sinh ra hóa đơn này. Null với các hóa đơn tạo thủ công từ trước
        /// khi Invoice trở thành chứng từ phái sinh từ Order (dữ liệu lịch sử).</summary>
        public Guid? OrderId { get; private set; }
        public string CustomerName { get; private set; } = string.Empty;
        public decimal Amount { get; private set; }
        public InvoiceStatus Status { get; private set; } = InvoiceStatus.Pending;

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>Dùng nội bộ (seed dữ liệu demo) — API công khai không còn cho tạo hóa đơn tay.</summary>
        public static Invoice Create(string customerName, decimal amount)
        {
            if (amount > 1_000_000_000)
                throw new DomainException("Hóa đơn không được vượt quá hạn mức 1,000,000,000 VNĐ.");

            return new Invoice
            {
                CustomerName = customerName,
                Amount = amount,
                Status = InvoiceStatus.Pending
            };
        }

        /// <summary>
        /// Sinh hóa đơn tự động ngay sau khi Order được Confirm (đã thanh toán qua saga
        /// Payment-Order) — hóa đơn ở đây là CHỨNG TỪ/BIÊN LAI ghi nhận đã thu tiền, không phải
        /// thứ cần thanh toán riêng, nên khởi tạo thẳng ở trạng thái Paid.
        /// </summary>
        public static Invoice CreateFromOrder(Guid orderId, string customerName, decimal amount)
        {
            if (amount > 1_000_000_000)
                throw new DomainException("Hóa đơn không được vượt quá hạn mức 1,000,000,000 VNĐ.");

            return new Invoice
            {
                OrderId = orderId,
                CustomerName = customerName,
                Amount = amount,
                Status = InvoiceStatus.Paid
            };
        }

        // ── Business Mutations ────────────────────────────────────────────────

        public void ChangeCustomerName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new DomainException("Tên khách hàng không được để trống.");

            CustomerName = newName;
            
            // Explicitly mark state as changed to trigger version increment
            MarkStateChanged();
        }

        public void UpdateStatus(InvoiceStatus newStatus)
        {
            if (Status == InvoiceStatus.Cancelled && newStatus != InvoiceStatus.Cancelled)
                throw new DomainException("Không thể thay đổi trạng thái của hóa đơn đã bị hủy.");

            Status = newStatus;
            
            // Explicitly mark state as changed
            MarkStateChanged();
        }

        /// <summary>
        /// Domain Reversal Method (Semantic Command)
        /// </summary>
        public void RestoreCustomerName(string previousName)
        {
            ChangeCustomerName(previousName);
        }
    }
}
