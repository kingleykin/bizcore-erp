using Bizcore.BuildingBlocks.Contracts;
using MassTransit;

namespace Customer.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận Fault&lt;OrderConfirmedEvent&gt; — MassTransit tự publish message này khi
    /// OrderConfirmedConsumer (cùng service) throw ở MỌI lần retry (5 lần, xem
    /// ApplyBusinessEndpointSettings) mà vẫn thất bại, tức lỗi cộng điểm là VĨNH VIỄN chứ không
    /// phải thoáng qua.
    ///
    /// Chỉ ở đây mới yêu cầu bồi hoàn (IPaymentCompensationRequestedEvent) để Payment.API chuyển
    /// Status=Reversed và Order.API tự Revert() đơn về ĐÚNG trạng thái trước khi thanh toán
    /// (Pending — không phải Cancelled) — KHÔNG làm việc này ngay trong
    /// OrderConfirmedConsumer.Consume(), vì một lỗi hạ tầng thoáng qua (mất kết nối DB...) hoàn toàn
    /// có thể tự khỏi ở lần retry sau; bồi hoàn ngay từ lần lỗi đầu tiên sẽ hủy/hoàn tiền oan cho một
    /// đơn thực ra vẫn có thể cộng điểm thành công nếu được thử lại.
    /// </summary>
    public class OrderConfirmedFaultConsumer : IConsumer<Fault<OrderConfirmedEvent>>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderConfirmedFaultConsumer> _logger;

        public OrderConfirmedFaultConsumer(IPublishEndpoint publishEndpoint, ILogger<OrderConfirmedFaultConsumer> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Fault<OrderConfirmedEvent>> context)
        {
            var original = context.Message.Message;

            if (original.PaymentId is not { } paymentId)
            {
                // Không nên xảy ra: OrderConfirmedConsumer đã return sớm (không throw) khi
                // PaymentId null, nên không thể fault ở đây — log để theo dõi nếu giả định sai.
                _logger.LogWarning(
                    "[Customer] Fault<OrderConfirmedEvent> for OrderId={OrderId} has no PaymentId, cannot request compensation.",
                    original.Id);
                return;
            }

            var reason = context.Message.Exceptions is { Length: > 0 } exceptions
                ? exceptions[0].Message
                : "Không rõ nguyên nhân";

            await _publishEndpoint.Publish<IPaymentCompensationRequestedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = (Guid?)original.Id,
                InvoiceId = (Guid?)null,
                Amount = original.TotalAmount,
                RequestedAt = DateTime.UtcNow,
                Reason = $"Cộng điểm khách hàng thất bại sau nhiều lần thử lại: {reason}"
            }, context.CancellationToken);

            _logger.LogError(
                "[Customer] Permanently failed to award points for OrderId={OrderId} after retries exhausted — requested payment compensation.",
                original.Id);
        }
    }
}
