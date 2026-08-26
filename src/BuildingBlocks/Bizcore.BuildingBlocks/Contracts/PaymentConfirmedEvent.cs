namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Payment service confirm payment
    /// sau khi Invoice/Order service đã validate thành công.
    /// </summary>
    public interface IConfirmPaymentCommand
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
    }

    /// <summary>
    /// Event: Payment service đã confirm payment (Status = Completed).
    /// Đây là event cuối cùng của happy path trong saga. Order.API lắng nghe event này
    /// (lọc theo OrderId != null) để tự động Confirm đơn hàng tương ứng.
    /// </summary>
    public interface IPaymentConfirmedEvent
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
        DateTime ConfirmedAt { get; }
    }
}
