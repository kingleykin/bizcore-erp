namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Yêu cầu bồi hoàn thanh toán — publish khi Payment.Status đã Completed nhưng bước xử lý sau
    /// đó (Order.Confirm hoặc Inventory.Commit) thất bại. OrderId/InvoiceId: đúng một trong hai có
    /// giá trị tùy loại thanh toán ban đầu (Order hay Invoice trực tiếp).
    /// </summary>
    public interface IPaymentCompensationRequestedEvent
    {
        Guid PaymentId { get; }
        Guid? OrderId { get; }
        Guid? InvoiceId { get; }
        decimal Amount { get; }
        DateTime RequestedAt { get; }
        string Reason { get; }
    }
}
