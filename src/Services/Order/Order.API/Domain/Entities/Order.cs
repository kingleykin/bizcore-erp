using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Order.API.Domain.Entities
{
    /// <summary>
    /// Đơn hàng của khách hàng (Order aggregate root).
    /// CustomerId/CustomerName lưu dạng snapshot tại thời điểm tạo đơn, vì Customer là
    /// một service/CSDL độc lập.
    /// </summary>
    public class Order : AggregateRoot
    {
        public string       OrderNumber  { get; private set; } = null!;
        public Guid         CustomerId   { get; private set; }
        public string       CustomerName { get; private set; } = null!;
        public DateTime     OrderDate    { get; private set; }
        public string?      Note         { get; private set; }
        public decimal      TotalAmount  { get; private set; }
        public OrderStatus  Status       { get; private set; } = OrderStatus.Pending;
        public string?      CancelReason { get; private set; }

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        private Order() { }

        public static Order Create(
            Guid customerId,
            string customerName,
            string? note,
            IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items)
        {
            if (customerId == Guid.Empty)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Khách hàng không được để trống.", new { field = nameof(CustomerId) });
            if (string.IsNullOrWhiteSpace(customerName))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên khách hàng không được để trống.", new { field = nameof(CustomerName) });

            var itemList = items?.ToList() ?? new List<(Guid, string, int, decimal)>();
            if (itemList.Count == 0)
                throw new DomainException(ErrorCodes.Order.EmptyItems, "Đơn hàng phải có ít nhất một sản phẩm.");

            var order = new Order
            {
                CustomerId   = customerId,
                CustomerName = customerName.Trim(),
                Note         = note?.Trim(),
                OrderDate    = DateTime.UtcNow,
                Status       = OrderStatus.Pending
            };
            order.OrderNumber = $"ORD{DateTime.UtcNow:yyyyMMdd}-{order.Id.ToString("N")[..6].ToUpperInvariant()}";

            foreach (var item in itemList)
                order._items.Add(OrderItem.Create(order.Id, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice));

            order.RecalculateTotal();

            return order;
        }

        private void RecalculateTotal()
        {
            TotalAmount = _items.Sum(i => i.Quantity * i.UnitPrice);
        }

        public void Confirm()
        {
            if (Status == OrderStatus.Cancelled)
                throw new DomainException(ErrorCodes.Order.InvalidStatus, "Không thể xác nhận đơn hàng đã bị hủy.");
            if (Status == OrderStatus.Confirmed)
                return;

            Status = OrderStatus.Confirmed;
            MarkStateChanged();
        }

        public void Cancel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Lý do hủy đơn không được để trống.", new { field = nameof(CancelReason) });
            if (Status != OrderStatus.Pending)
                throw new DomainException(ErrorCodes.Order.InvalidStatus, "Chỉ có thể hủy đơn hàng đang ở trạng thái chờ xử lý.");

            Status       = OrderStatus.Cancelled;
            CancelReason = reason.Trim();
            MarkStateChanged();
        }
    }
}
