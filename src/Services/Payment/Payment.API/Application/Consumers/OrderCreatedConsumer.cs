using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Consumers
{
    /// <summary>
    /// Đồng bộ read-model Order tối giản (chỉ để check tồn tại khi Initiate payment) — lắng nghe
    /// cùng OrderCreatedEvent mà Inventory.API đã dùng, Order.API không cần thay đổi gì để thêm
    /// subscriber mới này (MassTransit pub/sub hỗ trợ nhiều consumer cho cùng 1 event).
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
            var message = context.Message;

            var exists = await _context.Orders.AnyAsync(o => o.Id == message.Id, context.CancellationToken);
            if (exists)
            {
                _logger.LogInformation("Order {OrderId} already exists in Payment read model. Skipping.", message.Id);
                return;
            }

            _context.Orders.Add(new Order { Id = message.Id });
            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("Order {OrderId} synced to Payment read model.", message.Id);
        }
    }
}
