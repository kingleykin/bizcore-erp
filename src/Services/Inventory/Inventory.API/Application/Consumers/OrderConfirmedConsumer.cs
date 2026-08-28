using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Order được Confirm, chốt số đã giữ chỗ thành trừ kho thật cho từng sản phẩm.
    ///
    /// Nếu Commit() thất bại (DomainException — hiếm, chỉ còn xảy ra do race condition vì Order
    /// Service đã kiểm tra tồn kho trước khi tạo/xác nhận đơn), và đơn này được Confirm tự động do
    /// thanh toán (PaymentId có giá trị): yêu cầu bồi hoàn thanh toán qua
    /// IPaymentCompensationRequestedEvent thay vì để Payment/Order/Invoice kẹt ở trạng thái đã
    /// hoàn tất trong khi tồn kho chưa từng được trừ.
    /// </summary>
    public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderConfirmedConsumer> _logger;

        public OrderConfirmedConsumer(AppDbContext context, IPublishEndpoint publishEndpoint, ILogger<OrderConfirmedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
        {
            var msg = context.Message;

            try
            {
                foreach (var item in msg.Items)
                {
                    var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId, context.CancellationToken);
                    if (stock == null)
                    {
                        // Stock có thể chưa được tạo nếu OrderCreatedEvent (chạy trên endpoint khác) chưa
                        // xử lý xong — throw để MassTransit retry (UseMessageRetry), tránh mất vĩnh viễn
                        // việc trừ kho do bỏ qua âm thầm.
                        _logger.LogWarning(
                            "[Inventory] No stock record yet for ProductId={ProductId} when committing OrderId={OrderId}, will retry",
                            item.ProductId, msg.Id);
                        throw new InvalidOperationException(
                            $"Stock record for ProductId={item.ProductId} not found yet (OrderId={msg.Id}).");
                    }

                    stock.Commit(item.Quantity);

                    _context.StockTransactions.Add(Domain.Entities.StockTransaction.Create(
                        stock.ProductId,
                        stock.ProductName,
                        Domain.Entities.StockTransactionType.Commit,
                        quantity: -item.Quantity,
                        quantityOnHandAfter: stock.QuantityOnHand,
                        quantityReservedAfter: stock.QuantityReserved,
                        relatedOrderId: msg.Id));
                }
            }
            catch (DomainException ex)
            {
                _logger.LogError(ex,
                    "[Inventory] Failed to commit stock for OrderId={OrderId}, requesting payment compensation: {Reason}",
                    msg.Id, ex.Message);

                // Bỏ toàn bộ thay đổi (Commit) của các item đã xử lý trước item lỗi trong cùng đơn —
                // chưa SaveChanges nên Detach là đủ, đảm bảo cả đơn hoặc trừ kho hết, hoặc không trừ
                // gì cả (atomic), tránh trừ kho một phần rồi vẫn báo lỗi/yêu cầu bồi hoàn toàn đơn.
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                if (msg.PaymentId is { } paymentId)
                {
                    await _publishEndpoint.Publish<IPaymentCompensationRequestedEvent>(new
                    {
                        PaymentId = paymentId,
                        OrderId = (Guid?)msg.Id,
                        InvoiceId = (Guid?)null,
                        Amount = msg.TotalAmount,
                        RequestedAt = DateTime.UtcNow,
                        Reason = $"Không thể trừ kho khi chốt đơn hàng: {ex.Message}"
                    }, context.CancellationToken);

                    await _context.SaveChangesAsync(context.CancellationToken);
                }
                else
                {
                    // Confirm thủ công (không qua thanh toán) — không có payment để bồi hoàn,
                    // cần nhân viên đối soát thủ công.
                    _logger.LogError(
                        "[Inventory] OrderId={OrderId} was confirmed manually (no PaymentId) — cannot auto-compensate, needs manual reconciliation.",
                        msg.Id);
                }

                return;
            }

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Committed stock for OrderId={OrderId}, ItemCount={ItemCount}", msg.Id, msg.Items.Count);
        }
    }
}
