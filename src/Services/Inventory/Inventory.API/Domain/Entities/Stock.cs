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
        /// Giữ chỗ tồn kho khi đơn hàng được tạo (Pending). Order Service đã kiểm tra
        /// AvailableQuantity trước khi tạo đơn, nên trường hợp vượt tồn kho tới đây chỉ còn xảy ra
        /// do race condition giữa các đơn đồng thời — khi đó từ chối giữ chỗ để không để tồn kho âm.
        /// </summary>
        public void Reserve(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Số lượng giữ chỗ phải lớn hơn 0.");
            if (quantity > AvailableQuantity)
                throw new DomainException(
                    ErrorCodes.Inventory.InsufficientStock,
                    "Không đủ tồn kho khả dụng để giữ chỗ.",
                    new { productId = ProductId, available = AvailableQuantity, requested = quantity });

            QuantityReserved += quantity;
            MarkStateChanged();
        }

        /// <summary>
        /// Chốt số đã giữ chỗ thành trừ kho thật khi đơn hàng được Confirm (thường do thanh toán
        /// đơn hàng thành công). Kiểm tra so với QuantityReserved chứ không phải QuantityOnHand:
        /// Commit chỉ hiện thực hoá một cam kết giữ chỗ đã có từ trước (ở bước Reserve), không tạo
        /// nhu cầu mới, nên phải luôn cho phép hoàn tất các đơn đã giữ chỗ hợp lệ — kể cả những đơn
        /// được giữ chỗ từ trước khi có guard chống bán vượt tồn ở Reserve(), khi QuantityOnHand có
        /// thể đã âm sẵn do lịch sử. Việc chặn Commit trong trường hợp đó sẽ khiến thanh toán thành
        /// công nhưng đơn hàng kẹt vĩnh viễn, không giải quyết được tồn kho âm mà chỉ gây lỗi mới.
        /// </summary>
        public void Commit(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Số lượng chốt phải lớn hơn 0.");
            if (quantity > QuantityReserved)
                throw new DomainException(
                    ErrorCodes.Inventory.InsufficientStock,
                    "Số lượng chốt vượt quá số đã giữ chỗ cho đơn hàng.",
                    new { productId = ProductId, quantityReserved = QuantityReserved, requested = quantity });

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

        /// <summary>
        /// Nhập/điều chỉnh tồn kho vật lý thủ công (nhập hàng, kiểm kê). Không cho điều chỉnh
        /// xuống thấp hơn QuantityReserved — số đã giữ chỗ cho các đơn hàng Pending đang chờ xử lý —
        /// vì như vậy AvailableQuantity sẽ âm dù OnHand không âm.
        /// </summary>
        public void AdjustOnHand(int newQuantityOnHand)
        {
            if (newQuantityOnHand < 0)
                throw new DomainException(ErrorCodes.Inventory.InvalidQuantity, "Tồn kho không được âm.");
            if (newQuantityOnHand < QuantityReserved)
                throw new DomainException(
                    ErrorCodes.Inventory.InsufficientStock,
                    "Không thể điều chỉnh tồn kho thấp hơn số đã giữ chỗ cho đơn hàng đang chờ xử lý.",
                    new { productId = ProductId, quantityReserved = QuantityReserved, requested = newQuantityOnHand });

            QuantityOnHand = newQuantityOnHand;
            MarkStateChanged();
        }
    }
}
