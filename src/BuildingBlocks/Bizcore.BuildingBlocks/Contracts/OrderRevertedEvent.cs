namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order đã Confirm (thanh toán xong, tồn kho đã Commit) bị bồi hoàn về ĐÚNG trạng thái
    /// trước khi xử lý thanh toán (Pending) — publish bởi Order.API sau khi Order.Revert() thành
    /// công, khi nhận IPaymentCompensationRequestedEvent (vd. Customer.API cộng điểm thất bại vĩnh
    /// viễn). Inventory Service lắng nghe để đảo ngược Commit(): nhập lại cả OnHand lẫn Reserved
    /// (Stock.Uncommit) — khác OrderCancelledEvent (đơn hủy khi còn Pending, kho mới chỉ Reserve,
    /// nên chỉ cần Release() phần Reserved chứ không có OnHand để nhập lại).
    /// Dùng record (concrete type) — xem giải thích ở OrderCreatedEvent.
    /// </summary>
    public record OrderRevertedEvent(
        Guid Id,
        IReadOnlyCollection<OrderEventItem> Items,
        string Reason,
        DateTime RevertedAt
    );
}
