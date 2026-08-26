using System.ComponentModel.DataAnnotations.Schema;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Inventory.API.Domain.Entities
{
    /// <summary>
    /// Tồn kho của 1 sản phẩm. QuantityOnHand là tồn kho vật lý thực tế;
    /// QuantityReserved là số đã "giữ chỗ" cho các đơn hàng Pending nhưng chưa Confirm.
    /// Available (không lưu DB) = OnHand - Reserved, là số thực sự có thể bán thêm.
    /// </summary>
    public class Stock : AggregateRoot
    {
        public Guid    ProductId        { get; private set; }
        public string  ProductName      { get; private set; } = null!;
        public int     QuantityOnHand   { get; private set; }
        public int     QuantityReserved { get; private set; }

        [NotMapped]
        public int AvailableQuantity => QuantityOnHand - QuantityReserved;

        private Stock() { }

        public static Stock Create(Guid productId, string productName, int initialOnHand = 0)
        {
            if (productId == Guid.Empty)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Sản phẩm không được để trống.", new { field = nameof(ProductId) });
            if (string.IsNullOrWhiteSpace(productName))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên sản phẩm không được để trống.", new { field = nameof(ProductName) });
            if (initialOnHand < 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Tồn kho ban đầu không được âm.");

            return new Stock
            {
                ProductId = productId,
                ProductName = productName.Trim(),
                QuantityOnHand = initialOnHand,
                QuantityReserved = 0
            };
        }

        /// <summary>
        /// Giữ chỗ tồn kho khi đơn hàng được tạo (Pending). Đơn đã được tạo ở Order Service rồi
        /// nên bước này không thể "từ chối" đơn — nếu vượt tồn kho khả dụng vẫn giữ chỗ nhưng
        /// AvailableQuantity sẽ âm, phản ánh tình trạng bán vượt tồn (oversell) để người quản lý kho biết.
        /// </summary>
        public void Reserve(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Số lượng giữ chỗ phải lớn hơn 0.");

            QuantityReserved += quantity;
            MarkStateChanged();
        }

        /// <summary>Chốt số đã giữ chỗ thành trừ kho thật khi đơn hàng được Confirm.</summary>
        public void Commit(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Số lượng chốt phải lớn hơn 0.");

            QuantityOnHand -= quantity;
            QuantityReserved -= quantity;
            MarkStateChanged();
        }

        /// <summary>Trả lại số đã giữ chỗ khi đơn hàng bị hủy.</summary>
        public void Release(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Số lượng trả lại phải lớn hơn 0.");

            QuantityReserved = Math.Max(0, QuantityReserved - quantity);
            MarkStateChanged();
        }

        /// <summary>Nhập/điều chỉnh tồn kho vật lý thủ công (nhập hàng, kiểm kê).</summary>
        public void AdjustOnHand(int newQuantityOnHand)
        {
            if (newQuantityOnHand < 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Tồn kho không được âm.");

            QuantityOnHand = newQuantityOnHand;
            MarkStateChanged();
        }
    }
}
