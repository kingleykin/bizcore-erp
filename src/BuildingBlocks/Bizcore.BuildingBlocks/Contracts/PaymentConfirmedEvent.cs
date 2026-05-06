namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Payment service confirm payment
    /// sau khi Invoice service đã validate thành công.
    /// </summary>
    public interface IConfirmPaymentCommand
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
    }

    /// <summary>
    /// Event: Payment service đã confirm payment (Status = Completed).
    /// Đây là event cuối cùng của happy path trong saga.
    /// </summary>
    public interface IPaymentConfirmedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        DateTime ConfirmedAt { get; }
    }
}
