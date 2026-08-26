using Bizcore.BuildingBlocks.Contracts;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Order Service tạo đơn hàng mới (Pending), giữ chỗ (reserve) tồn kho cho từng sản phẩm
    /// trong đơn. Nếu sản phẩm chưa có bản ghi tồn kho (chưa từng nhập kho), tự tạo với OnHand = 0
    /// — khi đó AvailableQuantity sẽ âm, phản ánh đơn hàng đang bán vượt tồn kho thực tế.
    /// </summary>
    public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderCreatedConsumer> _logger;

        public OrderCreatedConsumer(AppDbContext context, ILogger<OrderCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            var msg = context.Message;

            foreach (var item in msg.Items)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId, context.CancellationToken);
                if (stock == null)
                {
                    _logger.LogWarning(
                        "[Inventory] No stock record for ProductId={ProductId}, creating with OnHand=0 (OrderId={OrderId})",
                        item.ProductId, msg.Id);
                    stock = Domain.Entities.Stock.Create(item.ProductId, $"Unknown ({item.ProductId})", initialOnHand: 0);
                    _context.Stocks.Add(stock);
                }

                stock.Reserve(item.Quantity);

                if (stock.AvailableQuantity < 0)
                {
                    _logger.LogWarning(
                        "[Inventory] Oversold ProductId={ProductId} after reserving OrderId={OrderId}: Available={Available}",
                        item.ProductId, msg.Id, stock.AvailableQuantity);
                }

                _context.StockTransactions.Add(Domain.Entities.StockTransaction.Create(
                    stock.ProductId,
                    stock.ProductName,
                    Domain.Entities.StockTransactionType.Reserve,
                    quantity: item.Quantity,
                    quantityOnHandAfter: stock.QuantityOnHand,
                    quantityReservedAfter: stock.QuantityReserved,
                    relatedOrderId: msg.Id));
            }

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Reserved stock for OrderId={OrderId}, ItemCount={ItemCount}", msg.Id, msg.Items.Count);
        }
    }
}
