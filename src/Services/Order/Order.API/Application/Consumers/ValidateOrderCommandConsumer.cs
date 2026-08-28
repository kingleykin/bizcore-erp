using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Application.Clients;
using Order.API.Infrastructure.Data;
using OrderEntity = Order.API.Domain.Entities.Order;

namespace Order.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IValidateOrderCommand từ Saga orchestrator (PaymentSaga) khi có người
    /// thanh toán cho một đơn hàng. Validate đơn còn Pending, số tiền khớp, và tồn kho vật lý
    /// (QuantityOnHand) còn đủ so với số lượng đặt trên từng dòng hàng — publish
    /// IOrderValidatedEvent/IOrderValidationFailedEvent tương ứng, cùng mô hình với
    /// ValidateInvoiceCommandConsumer bên Invoice.API.
    ///
    /// Kiểm tra tồn kho ở ĐÂY (trước khi Saga confirm thanh toán) thay vì chỉ dựa vào guard sẵn có
    /// ở Stock.Commit() (chạy SAU khi thanh toán đã Completed): nếu để tới Commit() mới phát hiện
    /// thiếu hàng, tiền đã thu rồi, phải bồi hoàn (IPaymentCompensationRequestedEvent) — trải nghiệm
    /// xấu hơn nhiều so với reject thanh toán ngay từ bước validate khi tiền chưa hề bị trừ.
    /// </summary>
    public class ValidateOrderCommandConsumer : IConsumer<IValidateOrderCommand>
    {
        private readonly AppDbContext _context;
        private readonly IInventoryServiceClient _inventoryClient;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ValidateOrderCommandConsumer> _logger;

        public ValidateOrderCommandConsumer(
            AppDbContext context,
            IInventoryServiceClient inventoryClient,
            IPublishEndpoint publishEndpoint,
            ILogger<ValidateOrderCommandConsumer> logger)
        {
            _context = context;
            _inventoryClient = inventoryClient;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IValidateOrderCommand> context)
        {
            var cmd = context.Message;

            _logger.LogInformation(
                "[Order] ValidateOrder command received PaymentId={PaymentId} OrderId={OrderId} Amount={Amount}",
                cmd.PaymentId, cmd.OrderId, cmd.Amount);

            var order = await _context.Orders.Include(o => o.Items).AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, context.CancellationToken);
            var error = Validate(order, cmd) ?? await ValidateStockAsync(order!, context.CancellationToken);

            if (error != null)
            {
                _logger.LogWarning(
                    "[Order] Order validation failed PaymentId={PaymentId} OrderId={OrderId}: {Reason}",
                    cmd.PaymentId, cmd.OrderId, error);

                await _publishEndpoint.Publish<IOrderValidationFailedEvent>(new
                {
                    cmd.PaymentId,
                    cmd.OrderId,
                    Reason = error,
                    FailedAt = DateTime.UtcNow
                }, context.CancellationToken);
                return;
            }

            _logger.LogInformation(
                "[Order] Order validated successfully PaymentId={PaymentId} OrderId={OrderId}",
                cmd.PaymentId, cmd.OrderId);

            await _publishEndpoint.Publish<IOrderValidatedEvent>(new
            {
                cmd.PaymentId,
                cmd.OrderId,
                ValidatedAt = DateTime.UtcNow
            }, context.CancellationToken);
        }

        private static string? Validate(OrderEntity? order, IValidateOrderCommand cmd)
        {
            if (order == null)
                return "Order not found.";

            if (order.Status == OrderStatus.Cancelled)
                return "Order is cancelled.";

            if (order.Status == OrderStatus.Confirmed)
                return "Order is already confirmed/paid.";

            if (order.Status != OrderStatus.Pending)
                return $"Order status '{order.Status}' is not valid for payment.";

            if (order.TotalAmount != cmd.Amount)
                return $"Amount mismatch: expected {order.TotalAmount}, got {cmd.Amount}.";

            return null;
        }

        /// <summary>
        /// Với mỗi sản phẩm trong đơn: chỉ báo lỗi và để saga hủy đơn khi tồn kho vật lý
        /// (QuantityOnHand) nhỏ hơn số lượng đặt trên đơn — đúng bằng tồn kho hiện có, không tính
        /// theo phần đã giữ chỗ (Reserved), theo yêu cầu nghiệp vụ.
        /// </summary>
        private async Task<string?> ValidateStockAsync(OrderEntity order, CancellationToken ct)
        {
            foreach (var item in order.Items)
            {
                var stock = await _inventoryClient.GetStockAsync(item.ProductId, ct);
                if (stock == null)
                    return $"Không tìm thấy tồn kho cho sản phẩm {item.ProductName}.";

                if (stock.QuantityOnHand < item.Quantity)
                    return $"Tồn kho không đủ để đảm bảo đơn hàng cho sản phẩm {item.ProductName} " +
                           $"(OnHand={stock.QuantityOnHand}, Quantity={item.Quantity}).";
            }

            return null;
        }
    }
}
