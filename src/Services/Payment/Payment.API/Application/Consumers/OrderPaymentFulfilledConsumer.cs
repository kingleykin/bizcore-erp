using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Payment.API.Application.Hubs;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IOrderPaymentFulfilledEvent từ Customer.API (bước cuối của chuỗi xử lý sau
    /// thanh toán Đơn hàng) — chuyển Payment.Status = Fulfilled (trạng thái CUỐI) và đẩy SignalR báo
    /// kết quả thành công cho khách hàng.
    ///
    /// Đây mới là nơi báo "thành công" cho luồng Đơn hàng — KHÔNG phải ConfirmPaymentConsumer: ở đó
    /// tiền mới chỉ được ghi nhận (Completed), các bước phía sau vẫn có thể kéo payment về Reversed.
    ///
    /// Chỉ nâng Completed -> Fulfilled. Nếu payment đã Reversed (bồi hoàn thắng trong một race hiếm,
    /// vd. Inventory.Commit lỗi chạy song song), KHÔNG được ghi đè ngược lại thành Fulfilled — bồi
    /// hoàn luôn là kết quả cuối cùng.
    /// </summary>
    public class OrderPaymentFulfilledConsumer : IConsumer<IOrderPaymentFulfilledEvent>
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly ILogger<OrderPaymentFulfilledConsumer> _logger;

        public OrderPaymentFulfilledConsumer(
            AppDbContext context,
            IHubContext<PaymentHub> hubContext,
            ILogger<OrderPaymentFulfilledConsumer> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IOrderPaymentFulfilledEvent> context)
        {
            var msg = context.Message;

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == msg.PaymentId, context.CancellationToken);
            if (payment == null)
            {
                _logger.LogWarning(
                    "[Payment] MarkFulfilled received but payment not found. PaymentId={PaymentId} OrderId={OrderId}",
                    msg.PaymentId, msg.OrderId);
                return;
            }

            if (payment.Status == PaymentStatus.Fulfilled)
            {
                _logger.LogInformation("[Payment] PaymentId={PaymentId} already fulfilled, skip (idempotent).", msg.PaymentId);
                return;
            }

            if (payment.Status != PaymentStatus.Completed)
            {
                _logger.LogWarning(
                    "[Payment] Skip marking PaymentId={PaymentId} as Fulfilled: current status is {Status} (chỉ nâng từ Completed; Reversed là kết quả cuối, không ghi đè).",
                    msg.PaymentId, payment.Status);
                return;
            }

            payment.Status = PaymentStatus.Fulfilled;
            await _context.SaveChangesAsync(context.CancellationToken);

            await _hubContext.Clients.Group(payment.Id.ToString()).SendAsync("PaymentStatusUpdated", new
            {
                PaymentId = payment.Id,
                Status = "Fulfilled",
                PaymentDate = payment.PaymentDate
            }, context.CancellationToken);

            _logger.LogInformation(
                "[Payment] PaymentId={PaymentId} fulfilled (OrderId={OrderId}) — toàn bộ chuỗi xử lý đã hoàn tất.",
                msg.PaymentId, msg.OrderId);
        }
    }
}
