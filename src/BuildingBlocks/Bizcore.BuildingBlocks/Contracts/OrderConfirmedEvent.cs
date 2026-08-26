namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order đã được xác nhận (coi như hoàn tất/đã thanh toán trong mô hình hiện tại).
    /// Inventory Service lắng nghe để chốt số đã giữ chỗ thành trừ kho thật (commit).
    /// Dùng record (concrete type) — xem giải thích ở OrderCreatedEvent.
    /// </summary>
    public record OrderConfirmedEvent(
        Guid Id,
        IReadOnlyCollection<OrderEventItem> Items,
        DateTime ConfirmedAt
    );
}
