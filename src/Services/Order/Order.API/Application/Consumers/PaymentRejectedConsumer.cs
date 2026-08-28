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
    /// Consumer nhận IPaymentRejectedEvent từ Payment.API (do PaymentSaga từ chối thanh toán vì
    /// validation thất bại hoặc timeout 60s) — event này dùng chung cho cả luồng Invoice lẫn Order
    /// nên phải lọc theo OrderId != null trước khi xử lý. Khi khớp, tự động Cancel đơn hàng đang
    /// Pending để trả lại (release) tồn kho đã giữ chỗ — nếu không, đơn sẽ kẹt Pending vĩnh viễn
    /// và tồn kho giữ chỗ cho nó không bao giờ được giải phóng dù thanh toán đã thất bại hẳn.
    ///
    /// KHÔNG gọi qua MediatR/CancelOrderCommand (ITransactionalCommand) — lý do giống
    /// PaymentConfirmedConsumer: MassTransit đã tự bọc Consume() trong 1 transaction sẵn.
    /// </summary>
    public class PaymentRejectedConsumer : IConsumer<IPaymentRejectedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<PaymentRejectedConsumer> _logger;

        public PaymentRejectedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<PaymentRejectedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentRejectedEvent> context)
        {
            var msg = context.Message;
            if (msg.OrderId is not { } orderId)
                return; // Thanh toán cho Invoice, không liên quan Order.API.

            var order = await _context.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, context.CancellationToken);

            if (order == null)
            {
                _logger.LogWarning(
                    "[Order] Could not auto-cancel OrderId={OrderId} after PaymentId={PaymentId} rejected: order not found",
                    orderId, msg.PaymentId);
                return;
            }

            if (order.Status != OrderStatus.Pending)
            {
                // Đơn đã Confirmed (race hiếm với nhánh xác nhận) hoặc đã Cancelled từ trước —
                // không có gì để release, chỉ log để theo dõi, không phải lỗi cần retry.
                _logger.LogWarning(
                    "[Order] Skip auto-cancel OrderId={OrderId} after PaymentId={PaymentId} rejected: status is {Status}",
                    orderId, msg.PaymentId, order.Status);
                return;
            }

            order.Cancel($"Thanh toán thất bại: {msg.Reason}");

            await _publishEndpoint.Publish(new OrderCancelledEvent(
                order.Id,
                order.Items.Select(i => new OrderEventItem(i.ProductId, i.Quantity)).ToList(),
                order.CancelReason!,
                DateTime.UtcNow
            ), context.CancellationToken);

            await _context.SaveChangesAsync(context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Order.Cancelled,
                entityType: "Order",
                entityId: order.Id.ToString(),
                after: new { order.Id, order.Status, order.CancelReason },
                category: AuditCategory.Business,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "[Order] Auto-cancelled OrderId={OrderId} after PaymentId={PaymentId} rejected: {Reason}",
                orderId, msg.PaymentId, msg.Reason);
        }
    }
}
