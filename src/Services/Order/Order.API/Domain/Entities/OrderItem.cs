using System.ComponentModel.DataAnnotations.Schema;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Order.API.Domain.Entities
{
    /// <summary>
    /// Dòng sản phẩm trong đơn hàng. ProductName là snapshot tại thời điểm đặt hàng
    /// (resolve từ Product Service ở Application layer), không đổi theo catalog về sau.
    /// </summary>
    public class OrderItem : BaseEntity
    {
        public Guid    OrderId     { get; private set; }
        public Guid    ProductId   { get; private set; }
        public string  ProductName { get; private set; } = null!;
        public int     Quantity    { get; private set; }
        public decimal UnitPrice   { get; private set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;

        // Navigation
        public Order Order { get; private set; } = null!;

        private OrderItem() { }

        public static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
        {
            if (productId == Guid.Empty)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Sản phẩm không được để trống.", new { field = nameof(ProductId) });
            if (string.IsNullOrWhiteSpace(productName))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên sản phẩm không được để trống.", new { field = nameof(ProductName) });
            if (quantity <= 0)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Số lượng phải lớn hơn 0.", new { field = nameof(Quantity) });
            if (unitPrice < 0)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Đơn giá không được âm.", new { field = nameof(UnitPrice) });

            return new OrderItem
            {
                OrderId     = orderId,
                ProductId   = productId,
                ProductName = productName.Trim(),
                Quantity    = quantity,
                UnitPrice   = unitPrice
            };
        }
    }
}
