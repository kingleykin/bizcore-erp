using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Infrastructure.Data;

namespace Order.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IPaymentCompensationRequestedEvent — publish khi một bước SAU khi Order đã
    /// Confirm (thanh toán xong) thất bại vĩnh viễn và cần bồi hoàn (vd. Customer.API cộng điểm
    /// thất bại sau khi hết số lần retry — xem Customer.API/OrderConfirmedFaultConsumer). Payment.API
    /// đồng thời nhận cùng event này để tự chuyển Payment.Status = Reversed.
    ///
    /// Đúng chuẩn compensating transaction: trả Order về CHÍNH XÁC trạng thái trước khi xử lý thanh
    /// toán (Pending), không phải Cancelled — xem Order.Revert(). Đơn có thể được thanh toán lại
    /// bình thường sau đó.
    ///
    /// Chỉ xử lý khi OrderId có giá trị — thanh toán Hóa đơn trực tiếp (InvoiceId) không rơi vào
    /// Order.API.
    ///
    /// Sau khi Revert() thành công, publish OrderRevertedEvent để Inventory Service nhập lại kho đã
    /// Commit (Stock.Uncommit) — KHÔNG publish OrderCancelledEvent: OrderCancelledConsumer bên
    /// Inventory.API chỉ biết Release() phần Reserved (đúng cho đơn hủy khi còn Pending, kho mới chỉ
    /// Reserve), không có nghiệp vụ nhập lại OnHand đã bị trừ thật — publish nhầm sẽ làm sai lệch
    /// tồn kho thay vì sửa đúng.
    ///
    /// KHÔNG đi qua MediatR/ITransactionalCommand — MassTransit đã tự bọc Consume() trong 1
    /// transaction sẵn (Transactional Inbox), giống mọi consumer khác trong Order.API.
    /// </summary>
    public class PaymentCompensationRequestedConsumer : IConsumer<IPaymentCompensationRequestedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<PaymentCompensationRequestedConsumer> _logger;

        public PaymentCompensationRequestedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<PaymentCompensationRequestedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
        {
            var msg = context.Message;
            if (msg.OrderId is not { } orderId)
                return;

            var order = await _context.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, context.CancellationToken);
            if (order == null)
            {
                _logger.LogWarning(
                    "[Order] Could not revert OrderId={OrderId} for compensation: order not found.", orderId);
                return;
            }

            try
            {
                order.Revert();
            }
            catch (DomainException ex)
            {
                // Đơn không còn ở Confirmed — MassTransit's EF Inbox đã tự loại redelivery đúng
                // nghĩa (cùng MessageId) nên trường hợp này chỉ còn xảy ra do race hiếm ở tầng
                // nghiệp vụ: 2 yêu cầu bồi hoàn khác nhau cho cùng đơn, hoặc đơn đã bị Cancelled/
                // Revert bởi một luồng khác. Không phải lỗi thoáng qua nên không throw để retry vô
                // ích, chỉ log để đối soát.
                _logger.LogWarning(
                    "[Order] Could not revert OrderId={OrderId} (current status may already reflect the compensation, or a rare race occurred): {Reason}",
                    orderId, ex.Message);
                return;
            }

            await _publishEndpoint.Publish(new OrderRevertedEvent(
                order.Id,
                order.Items.Select(i => new OrderEventItem(i.ProductId, i.Quantity)).ToList(),
                msg.Reason,
                DateTime.UtcNow
            ), context.CancellationToken);

            await _context.SaveChangesAsync(context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Order.Reverted,
                entityType: "Order",
                entityId: order.Id.ToString(),
                after: new { order.Id, order.Status, Reason = msg.Reason },
                category: AuditCategory.Business,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogWarning(
                "[Order] Reverted OrderId={OrderId} back to Pending due to payment compensation: {Reason}", orderId, msg.Reason);
        }
    }
}
