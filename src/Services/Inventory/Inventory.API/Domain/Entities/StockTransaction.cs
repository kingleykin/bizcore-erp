using Bizcore.BuildingBlocks.Abstractions;

namespace Inventory.API.Domain.Entities
{
    public enum StockTransactionType
    {
        Reserve,
        Commit,
        Release,
        Adjust
    }

    /// <summary>
    /// Bản ghi lịch sử một lần thay đổi tồn kho (xuất/nhập/giữ chỗ/trả chỗ/điều chỉnh).
    /// Append-only — không có nghiệp vụ cập nhật/xóa.
    /// </summary>
    public class StockTransaction : BaseEntity
    {
        public Guid                 ProductId             { get; private set; }
        public string                ProductName           { get; private set; } = null!;
        public StockTransactionType Type                  { get; private set; }
        public int                  Quantity              { get; private set; }
        public int                  QuantityOnHandAfter   { get; private set; }
        public int                  QuantityReservedAfter { get; private set; }
        public Guid?                RelatedOrderId        { get; private set; }
        public string?              Note                  { get; private set; }

        private StockTransaction() { }

        public static StockTransaction Create(
            Guid productId,
            string productName,
            StockTransactionType type,
            int quantity,
            int quantityOnHandAfter,
            int quantityReservedAfter,
            Guid? relatedOrderId = null,
            string? note = null)
        {
            return new StockTransaction
            {
                ProductId = productId,
                ProductName = productName,
                Type = type,
                Quantity = quantity,
                QuantityOnHandAfter = quantityOnHandAfter,
                QuantityReservedAfter = quantityReservedAfter,
                RelatedOrderId = relatedOrderId,
                Note = note
            };
        }
    }
}
