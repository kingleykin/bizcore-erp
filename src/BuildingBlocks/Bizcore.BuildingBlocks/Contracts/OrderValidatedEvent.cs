namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order service đã validate đơn hàng thành công (còn Pending, số tiền khớp).
    /// Saga orchestrator nhận event này để confirm payment.
    /// </summary>
    public interface IOrderValidatedEvent
    {
        Guid PaymentId { get; }
        Guid OrderId { get; }
        DateTime ValidatedAt { get; }
    }

    /// <summary>
    /// Event: Order service từ chối validation (đơn không tồn tại, đã Confirmed/Cancelled,
    /// số tiền không khớp, v.v.). Saga orchestrator nhận event này để reject payment.
    /// </summary>
    public interface IOrderValidationFailedEvent
    {
        Guid PaymentId { get; }
        Guid OrderId { get; }
        string Reason { get; }
        DateTime FailedAt { get; }
    }
}
