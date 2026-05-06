namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Payment service đã tạo payment record với trạng thái Processing,
    /// chờ Saga orchestrator điều phối validation với Invoice service.
    /// </summary>
    public interface IPaymentInitiatedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        decimal Amount { get; }
        string IdempotencyKey { get; }
        DateTime InitiatedAt { get; }
    }
}
