using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận OrderConfirmedEvent từ Order.API — cộng điểm thưởng cho khách hàng khi đơn
    /// hàng THỰC SỰ đã thanh toán thành công (PaymentId có giá trị; Confirm thủ công qua API không
    /// cộng điểm vì không có bằng chứng đã thanh toán). Quy tắc: đơn > 1.000.000đ được +5 điểm,
    /// còn lại +1 điểm.
    ///
    /// Idempotent theo OrderId (CustomerPointsTransactions.OrderId unique) — an toàn nếu
    /// OrderConfirmedEvent bị MassTransit redeliver, không cộng điểm lặp lại.
    ///
    /// Xử lý khi cộng điểm thất bại: thanh toán/đơn hàng đã hoàn tất từ trước ở Payment.API/
    /// Order.API, hoàn toàn độc lập với bước này — nên KHÔNG có compensation nào cần thiết ở đây
    /// (khác với việc trừ kho, cộng điểm chỉ là quyền lợi phụ trợ, không phải nghiệp vụ phải đảo
    /// ngược nếu thất bại). Chỉ cần để exception bay lên cho MassTransit UseMessageRetry tự retry
    /// theo policy cấu hình sẵn; hết retry thì message vào error queue chờ xử lý thủ công thay vì
    /// mất điểm thưởng của khách hàng một cách âm thầm.
    /// </summary>
    public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
    {
        private const decimal HighValueThreshold = 1_000_000m;
        private const int HighValuePoints = 5;
        private const int StandardPoints = 1;

        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<OrderConfirmedConsumer> _logger;

        public OrderConfirmedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<OrderConfirmedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>
        /// Báo cho Payment.API biết chuỗi xử lý sau thanh toán đã kết thúc và KHÔNG bị bồi hoàn —
        /// đây là bước cuối của chuỗi, nên Payment.API dùng tín hiệu này để chốt Fulfilled và báo
        /// kết quả cuối cho khách hàng (xem IOrderPaymentFulfilledEvent). Gọi ở MỌI đường ra "coi như
        /// xong" của consumer này (cộng điểm thành công, đã cộng từ trước, hoặc bỏ qua vì không tìm
        /// thấy khách hàng): thiếu một đường sẽ khiến khách bị treo màn hình chờ tới khi hết timeout
        /// dù giao dịch thực ra đã xong.
        /// </summary>
        private Task PublishFulfilledAsync(Guid paymentId, Guid orderId, CancellationToken ct) =>
            _publishEndpoint.Publish<IOrderPaymentFulfilledEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                FulfilledAt = DateTime.UtcNow
            }, ct);

        public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
        {
            var msg = context.Message;

            if (msg.PaymentId is null)
            {
                _logger.LogDebug(
                    "[Customer] OrderId={OrderId} confirmed manually (no PaymentId) — skip loyalty points.",
                    msg.Id);
                return;
            }

            var paymentId = msg.PaymentId.Value;

            var alreadyAwarded = await _context.CustomerPointsTransactions
                .AnyAsync(t => t.OrderId == msg.Id, context.CancellationToken);
            if (alreadyAwarded)
            {
                _logger.LogInformation(
                    "[Customer] Points already awarded for OrderId={OrderId}, skip (idempotent).", msg.Id);
                await PublishFulfilledAsync(paymentId, msg.Id, context.CancellationToken);
                return;
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == msg.CustomerId, context.CancellationToken);
            if (customer == null)
            {
                // Order.API đã xác thực CustomerId tồn tại trước khi tạo đơn, nên trường hợp này
                // gần như không xảy ra trừ khi khách hàng bị xoá sau đó — không throw để retry vô ích,
                // chỉ log để nhân viên đối soát thủ công. Vẫn coi là "xong" với thanh toán (không bồi
                // hoàn), nên phải báo Fulfilled để khách không bị treo màn hình chờ.
                _logger.LogError(
                    "[Customer] CustomerId={CustomerId} not found when awarding points for OrderId={OrderId} — needs manual reconciliation.",
                    msg.CustomerId, msg.Id);
                await PublishFulfilledAsync(paymentId, msg.Id, context.CancellationToken);
                return;
            }

            var points = msg.TotalAmount > HighValueThreshold ? HighValuePoints : StandardPoints;
            customer.AddPoints(points);

            _context.CustomerPointsTransactions.Add(
                Domain.Entities.CustomerPointsTransaction.Create(customer.Id, msg.Id, points, customer.Points));

            await _context.SaveChangesAsync(context.CancellationToken);

            await PublishFulfilledAsync(paymentId, msg.Id, context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Customer.PointsAwarded,
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                after: new { customer.Id, PointsAwarded = points, customer.Points, OrderId = msg.Id },
                category: AuditCategory.Business,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "[Customer] Awarded {Points} points to CustomerId={CustomerId} for OrderId={OrderId} (TotalAmount={TotalAmount}). New balance={Balance}.",
                points, customer.Id, msg.Id, msg.TotalAmount, customer.Points);
        }
    }
}
