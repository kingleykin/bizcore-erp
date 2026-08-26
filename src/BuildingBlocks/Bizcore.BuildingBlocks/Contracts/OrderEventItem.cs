namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Snapshot 1 dòng sản phẩm trong đơn hàng, dùng chung cho các event vòng đời Order
    /// (OrderCreated/OrderConfirmed/OrderCancelled) để Inventory Service biết cần
    /// giữ chỗ/trừ kho/trả kho cho sản phẩm nào với số lượng bao nhiêu.
    /// </summary>
    public record OrderEventItem(Guid ProductId, int Quantity);
}
