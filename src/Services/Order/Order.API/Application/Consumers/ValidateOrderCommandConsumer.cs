using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Infrastructure.Data;
using OrderEntity = Order.API.Domain.Entities.Order;

namespace Order.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IValidateOrderCommand từ Saga orchestrator (PaymentSaga) khi có người
    /// thanh toán cho một đơn hàng. Validate đơn còn Pending và số tiền khớp, publish
    /// IOrderValidatedEvent/IOrderValidationFailedEvent tương ứng — cùng mô hình với
    /// ValidateInvoiceCommandConsumer bên Invoice.API.
    /// </summary>
    public class ValidateOrderCommandConsumer : IConsumer<IValidateOrderCommand>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ValidateOrderCommandConsumer> _logger;

        public ValidateOrderCommandConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<ValidateOrderCommandConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IValidateOrderCommand> context)
        {
            var cmd = context.Message;

            _logger.LogInformation(
                "[Order] ValidateOrder command received PaymentId={PaymentId} OrderId={OrderId} Amount={Amount}",
                cmd.PaymentId, cmd.OrderId, cmd.Amount);

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == cmd.OrderId, context.CancellationToken);
            var error = Validate(order, cmd);

            if (error != null)
            {
                _logger.LogWarning(
                    "[Order] Order validation failed PaymentId={PaymentId} OrderId={OrderId}: {Reason}",
                    cmd.PaymentId, cmd.OrderId, error);

                await _publishEndpoint.Publish<IOrderValidationFailedEvent>(new
                {
                    cmd.PaymentId,
                    cmd.OrderId,
                    Reason = error,
                    FailedAt = DateTime.UtcNow
                }, context.CancellationToken);
                return;
            }

            _logger.LogInformation(
                "[Order] Order validated successfully PaymentId={PaymentId} OrderId={OrderId}",
                cmd.PaymentId, cmd.OrderId);

            await _publishEndpoint.Publish<IOrderValidatedEvent>(new
            {
                cmd.PaymentId,
                cmd.OrderId,
                ValidatedAt = DateTime.UtcNow
            }, context.CancellationToken);
        }

        private static string? Validate(OrderEntity? order, IValidateOrderCommand cmd)
        {
            if (order == null)
                return "Order not found.";

            if (order.Status == OrderStatus.Cancelled)
                return "Order is cancelled.";

            if (order.Status == OrderStatus.Confirmed)
                return "Order is already confirmed/paid.";

            if (order.Status != OrderStatus.Pending)
                return $"Order status '{order.Status}' is not valid for payment.";

            if (order.TotalAmount != cmd.Amount)
                return $"Amount mismatch: expected {order.TotalAmount}, got {cmd.Amount}.";

            return null;
        }
    }
}
