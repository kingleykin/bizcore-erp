namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Payment service reject payment
    /// sau khi Invoice/Order service validation failed (hoặc timeout).
    /// </summary>
    public interface IRejectPaymentCommand
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Event: Payment service đã reject payment (Status = Failed).
    /// Đây là event cuối cùng của failure path trong saga. Với luồng Order, đơn hàng
    /// vẫn giữ nguyên Pending (không tự động Cancel) — khách/nhân viên có thể thử thanh
    /// toán lại hoặc chủ động Hủy đơn.
    /// </summary>
    public interface IPaymentRejectedEvent
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
        string Reason { get; }
        DateTime RejectedAt { get; }
    }
}
