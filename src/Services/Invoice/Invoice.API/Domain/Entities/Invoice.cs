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
        public string CustomerName { get; private set; } = string.Empty;
        public decimal Amount { get; private set; }
        public InvoiceStatus Status { get; private set; } = InvoiceStatus.Pending;

        // ── Factory ───────────────────────────────────────────────────────────

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
