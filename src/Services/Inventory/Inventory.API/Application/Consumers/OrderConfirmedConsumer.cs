using Bizcore.BuildingBlocks.Contracts;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Order được Confirm, chốt số đã giữ chỗ thành trừ kho thật cho từng sản phẩm.
    /// </summary>
    public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderConfirmedConsumer> _logger;

        public OrderConfirmedConsumer(AppDbContext context, ILogger<OrderConfirmedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
        {
            var msg = context.Message;

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

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Committed stock for OrderId={OrderId}, ItemCount={ItemCount}", msg.Id, msg.Items.Count);
        }
    }
}
