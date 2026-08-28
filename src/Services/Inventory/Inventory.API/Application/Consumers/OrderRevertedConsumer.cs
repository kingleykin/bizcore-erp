using Bizcore.BuildingBlocks.Contracts;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Order đã Confirm (đã Commit — trừ kho thật) bị bồi hoàn về Pending (vd. Customer.API
    /// cộng điểm thất bại vĩnh viễn — xem Order.API/PaymentCompensationRequestedConsumer), nhập lại
    /// đúng số lượng đã trừ cho từng sản phẩm — đối xứng với OrderConfirmedConsumer (Commit).
    ///
    /// Dùng Stock.Uncommit() chứ không phải Release(): Release() chỉ trả lại phần Reserved (đúng
    /// cho đơn Cancelled khi còn Pending, kho mới chỉ giữ chỗ); ở đây kho đã bị trừ OnHand thật nên
    /// phải nhập lại cả OnHand lẫn Reserved để đơn trở lại đúng trạng thái "Pending, đã giữ chỗ"
    /// như trước lúc Confirm.
    /// </summary>
    public class OrderRevertedConsumer : IConsumer<OrderRevertedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderRevertedConsumer> _logger;

        public OrderRevertedConsumer(AppDbContext context, ILogger<OrderRevertedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderRevertedEvent> context)
        {
            var msg = context.Message;

            foreach (var item in msg.Items)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId, context.CancellationToken);
                if (stock == null)
                {
                    // Xem giải thích tương tự ở OrderConfirmedConsumer/OrderCancelledConsumer: throw
                    // để MassTransit retry thay vì bỏ qua âm thầm (mất vĩnh viễn việc nhập lại kho).
                    _logger.LogWarning(
                        "[Inventory] No stock record yet for ProductId={ProductId} when uncommitting OrderId={OrderId}, will retry",
                        item.ProductId, msg.Id);
                    throw new InvalidOperationException(
                        $"Stock record for ProductId={item.ProductId} not found yet (OrderId={msg.Id}).");
                }

                stock.Uncommit(item.Quantity);

                _context.StockTransactions.Add(Domain.Entities.StockTransaction.CreateFor(
                    stock, Domain.Entities.StockTransactionType.Uncommit, quantity: item.Quantity, relatedOrderId: msg.Id));
            }

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Uncommitted stock for OrderId={OrderId}, ItemCount={ItemCount}", msg.Id, msg.Items.Count);
        }
    }
}
