using Bizcore.BuildingBlocks.Contracts;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Order bị hủy (chỉ xảy ra lúc còn Pending), trả lại số đã giữ chỗ cho từng sản phẩm.
    /// </summary>
    public class OrderCancelledConsumer : IConsumer<OrderCancelledEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderCancelledConsumer> _logger;

        public OrderCancelledConsumer(AppDbContext context, ILogger<OrderCancelledConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
        {
            var msg = context.Message;

            foreach (var item in msg.Items)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId, context.CancellationToken);
                if (stock == null)
                {
                    // Xem giải thích ở OrderConfirmedConsumer: throw để retry thay vì bỏ qua âm thầm.
                    _logger.LogWarning(
                        "[Inventory] No stock record yet for ProductId={ProductId} when releasing OrderId={OrderId}, will retry",
                        item.ProductId, msg.Id);
                    throw new InvalidOperationException(
                        $"Stock record for ProductId={item.ProductId} not found yet (OrderId={msg.Id}).");
                }

                stock.Release(item.Quantity);

                _context.StockTransactions.Add(Domain.Entities.StockTransaction.Create(
                    stock.ProductId,
                    stock.ProductName,
                    Domain.Entities.StockTransactionType.Release,
                    quantity: item.Quantity,
                    quantityOnHandAfter: stock.QuantityOnHand,
                    quantityReservedAfter: stock.QuantityReserved,
                    relatedOrderId: msg.Id));
            }

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Released stock for OrderId={OrderId}, ItemCount={ItemCount}", msg.Id, msg.Items.Count);
        }
    }
}
