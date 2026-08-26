namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Payment service đã tạo payment record với trạng thái Processing,
    /// chờ Saga orchestrator điều phối validation với Invoice hoặc Order service.
    /// Đúng một trong hai InvoiceId/OrderId được set — Payment có thể trả cho hóa đơn
    /// (luồng cũ) hoặc trả cho đơn hàng (luồng Order mới), không bao giờ cả hai.
    /// </summary>
    public interface IPaymentInitiatedEvent
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
        decimal Amount { get; }
        string IdempotencyKey { get; }
        DateTime InitiatedAt { get; }
    }
}
