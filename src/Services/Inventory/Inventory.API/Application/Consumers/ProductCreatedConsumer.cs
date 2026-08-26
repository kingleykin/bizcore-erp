using Bizcore.BuildingBlocks.Contracts;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Consumers
{
    /// <summary>
    /// Khi Product Service tạo sản phẩm mới, tạo sẵn bản ghi tồn kho (OnHand=0) cho sản phẩm đó
    /// — để sản phẩm xuất hiện ngay ở màn hình Kho hàng thay vì chỉ xuất hiện sau khi có đơn hàng đầu tiên.
    /// </summary>
    public class ProductCreatedConsumer : IConsumer<IProductCreatedEvent>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductCreatedConsumer> _logger;

        public ProductCreatedConsumer(AppDbContext context, ILogger<ProductCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IProductCreatedEvent> context)
        {
            var msg = context.Message;

            var exists = await _context.Stocks.AnyAsync(s => s.ProductId == msg.Id, context.CancellationToken);
            if (exists)
                return;

            var stock = Domain.Entities.Stock.Create(msg.Id, msg.Name, initialOnHand: 0);
            _context.Stocks.Add(stock);

            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("[Inventory] Created stock record for new ProductId={ProductId}, Name={Name}", msg.Id, msg.Name);
        }
    }
}
