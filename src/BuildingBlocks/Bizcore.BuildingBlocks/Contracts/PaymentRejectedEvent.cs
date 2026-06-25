namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Payment service reject payment
    /// sau khi Invoice service validation failed.
    /// </summary>
    public interface IRejectPaymentCommand
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Event: Payment service đã reject payment (Status = Failed).
    /// Đây là event cuối cùng của failure path trong saga.
    /// </summary>
    public interface IPaymentRejectedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
        DateTime RejectedAt { get; }
    }


}
