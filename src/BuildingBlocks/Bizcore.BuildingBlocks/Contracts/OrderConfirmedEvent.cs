namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order đã được xác nhận (coi như hoàn tất/đã thanh toán trong mô hình hiện tại).
    /// Inventory Service lắng nghe để chốt số đã giữ chỗ thành trừ kho thật (commit).
    /// Invoice Service lắng nghe để tự sinh Invoice (chứng từ/biên lai) phái sinh từ Order —
    /// cần CustomerName/TotalAmount ở đây để không phải gọi ngược sang Order.API qua HTTP.
    /// Dùng record (concrete type) — xem giải thích ở OrderCreatedEvent.
    /// </summary>
    public record OrderConfirmedEvent(
        Guid Id,
        string CustomerName,
        decimal TotalAmount,
        IReadOnlyCollection<OrderEventItem> Items,
        DateTime ConfirmedAt
    );
}
