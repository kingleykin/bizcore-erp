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
    /// Payment.Status = Completed từng được đẩy real-time cho khách qua SignalR (ConfirmPaymentConsumer)
    /// TRƯỚC KHI các bước xử lý phía sau (Order.Confirm, Inventory.Commit, Customer cộng điểm) chạy
    /// xong — nếu 1 trong các bước đó thất bại vĩnh viễn và yêu cầu bồi hoàn (event này), khách đã
    /// thấy "thanh toán thành công" trước đó rồi. Nếu không báo lại real-time ở đây, khách sẽ không
    /// biết giao dịch vừa bị hoàn cho tới khi tự làm mới trang — đúng rủi ro UX/nghiệp vụ đã gặp
    /// (thấy "thành công" nhưng sau đó âm thầm đổi kết quả). Đẩy cùng sự kiện "PaymentStatusUpdated"
    /// như ConfirmPaymentConsumer/RejectPaymentConsumer để client đang lắng nghe (WatchPayment) nhận
    /// được ngay.
    /// </summary>
    public class PaymentCompensationRequestedConsumer : IConsumer<IPaymentCompensationRequestedEvent>
    {
        private readonly AppDbContext _context;
        private readonly Payment.API.Infrastructure.Telemetry.PaymentMetrics _metrics;
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly ILogger<PaymentCompensationRequestedConsumer> _logger;

        public PaymentCompensationRequestedConsumer(
            AppDbContext context,
            Payment.API.Infrastructure.Telemetry.PaymentMetrics metrics,
            IHubContext<PaymentHub> hubContext,
            ILogger<PaymentCompensationRequestedConsumer> logger)
        {
            _context = context;
            _metrics = metrics;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
        {
            var message = context.Message;

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == message.PaymentId);
            if (payment == null)
            {
                _logger.LogWarning(
                    "Compensation requested but payment not found. PaymentId: {PaymentId}, InvoiceId: {InvoiceId}",
                    message.PaymentId,
                    message.InvoiceId);
                return;
            }

            if (payment.Status == PaymentStatus.Reversed)
            {
                _logger.LogInformation("Payment {PaymentId} is already reversed. Skipping.", message.PaymentId);
                return;
            }

            payment.Status = PaymentStatus.Reversed;
            await _context.SaveChangesAsync();

            // Record Metric
            _metrics.PaymentReversed();

            // SignalR Push Notification (UX Enhancement Layer) — xem giải thích ở class doc: đây là
            // lần cập nhật trạng thái THỨ HAI cho cùng payment (sau "Completed"), khách hàng bắt buộc
            // phải được báo lại, không được để im lặng.
            await _hubContext.Clients.Group(payment.Id.ToString()).SendAsync("PaymentStatusUpdated", new
            {
                PaymentId = payment.Id,
                Status = "Reversed",
                FailureReason = message.Reason
            });

            _logger.LogInformation(
                "Payment reversed successfully. PaymentId: {PaymentId}, InvoiceId: {InvoiceId}, Reason: {Reason}",
                message.PaymentId,
                message.InvoiceId,
                message.Reason);
        }
    }
}
