namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order bị hủy khi còn ở trạng thái Pending.
    /// Inventory Service lắng nghe để trả lại số lượng đã giữ chỗ (release) cho từng sản phẩm.
    /// Dùng record (concrete type) — xem giải thích ở OrderCreatedEvent.
    /// </summary>
    public record OrderCancelledEvent(
        Guid Id,
        IReadOnlyCollection<OrderEventItem> Items,
        string Reason,
        DateTime CancelledAt
    );
}
