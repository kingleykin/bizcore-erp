namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order service đã tạo đơn hàng mới (trạng thái Pending).
    /// Inventory Service lắng nghe để giữ chỗ (reserve) tồn kho cho từng sản phẩm trong đơn.
    /// Dùng record (concrete type) thay vì interface: message có collection lồng nhau
    /// (Items) — publish/consume qua interface message contract của MassTransit (dynamic
    /// proxy) không bind đúng property dạng collection, khiến Items luôn null phía consumer.
    /// </summary>
    public record OrderCreatedEvent(
        Guid Id,
        Guid CustomerId,
        string CustomerName,
        string OrderNumber,
        decimal TotalAmount,
        IReadOnlyCollection<OrderEventItem> Items,
        DateTime CreatedAt
    );
}
