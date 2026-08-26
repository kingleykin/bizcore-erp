namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Product service đã tạo sản phẩm mới.
    /// Inventory Service lắng nghe để tạo sẵn bản ghi tồn kho (OnHand=0) cho sản phẩm,
    /// thay vì chỉ tạo lazily khi có đơn hàng đầu tiên — giúp sản phẩm mới xuất hiện ngay ở màn hình Kho hàng.
    /// </summary>
    public interface IProductCreatedEvent
    {
        Guid Id { get; }
        string Name { get; }
        DateTime CreatedAt { get; }
    }
}
