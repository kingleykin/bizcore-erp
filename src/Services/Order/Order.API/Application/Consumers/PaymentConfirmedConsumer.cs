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
    /// Consumer nhận IPaymentConfirmedEvent từ Payment.API — event này dùng chung cho cả
    /// luồng Invoice lẫn Order nên phải lọc theo OrderId != null trước khi xử lý (bỏ qua nếu
    /// đây là thanh toán cho Invoice). Khi khớp, tự động Confirm đơn hàng tương ứng.
    ///
    /// QUAN TRỌNG: KHÔNG gọi qua MediatR/ConfirmOrderCommand (ITransactionalCommand) — MassTransit
    /// đã tự bọc Consume() trong 1 transaction sẵn (Transactional Inbox), nếu TransactionBehavior
    /// mở thêm 1 transaction nữa trên cùng connection sẽ throw
    /// "The connection is already in a transaction". Vì vậy thao tác thẳng qua AppDbContext,
    /// tự SaveChangesAsync — giống mọi consumer khác trong hệ thống.
    /// </summary>
    public class PaymentConfirmedConsumer : IConsumer<IPaymentConfirmedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<PaymentConfirmedConsumer> _logger;

        public PaymentConfirmedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<PaymentConfirmedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentConfirmedEvent> context)
        {
            var msg = context.Message;
            if (msg.OrderId is not { } orderId)
                return; // Thanh toán cho Invoice, không liên quan Order.API.

            var order = await _context.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, context.CancellationToken);

            if (order == null)
            {
                // Không nên xảy ra vì Payment chỉ Confirm sau khi ValidateOrderCommand đã xác nhận
                // đơn tồn tại, nhưng vẫn log để theo dõi.
                _logger.LogWarning(
                    "[Order] Could not auto-confirm OrderId={OrderId} after PaymentId={PaymentId}: order not found",
                    orderId, msg.PaymentId);
                return;
            }

            var wasAlreadyConfirmed = order.Status == OrderStatus.Confirmed;
            try
            {
                order.Confirm();
            }
            catch (DomainException ex)
            {
                // Đơn có thể đã bị Hủy tay trong lúc chờ thanh toán (race hiếm) — không phải lỗi
                // thoáng qua nên không cần retry, chỉ log để nhân viên xử lý thủ công (đối soát).
                _logger.LogWarning(ex,
                    "[Order] Could not auto-confirm OrderId={OrderId} after PaymentId={PaymentId}: {Reason}",
                    orderId, msg.PaymentId, ex.Message);
                return;
            }

            if (!wasAlreadyConfirmed)
            {
                await _publishEndpoint.Publish(new OrderConfirmedEvent(
                    order.Id,
                    order.Items.Select(i => new OrderEventItem(i.ProductId, i.Quantity)).ToList(),
                    DateTime.UtcNow
                ), context.CancellationToken);
            }

            await _context.SaveChangesAsync(context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Order.Confirmed,
                entityType: "Order",
                entityId: order.Id.ToString(),
                after: new { order.Id, order.Status },
                category: AuditCategory.Business,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "[Order] Auto-confirmed OrderId={OrderId} after PaymentId={PaymentId} completed",
                orderId, msg.PaymentId);
        }
    }
}
