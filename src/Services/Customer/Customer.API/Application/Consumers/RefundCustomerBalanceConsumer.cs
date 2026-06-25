using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IRefundCustomerBalanceCommand từ Saga khi rollback.
    /// Hoàn tiền lại vào tài khoản khách hàng.
    /// </summary>
    public class RefundCustomerBalanceConsumer : IConsumer<IRefundCustomerBalanceCommand>
    {
        private readonly CustomerDbContext _context;
        private readonly Bizcore.BuildingBlocks.Audit.IAuditPublisher _audit;
        private readonly ILogger<RefundCustomerBalanceConsumer> _logger;

        public RefundCustomerBalanceConsumer(CustomerDbContext context, Bizcore.BuildingBlocks.Audit.IAuditPublisher audit, ILogger<RefundCustomerBalanceConsumer> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IRefundCustomerBalanceCommand> context)
        {
            var message = context.Message;
            _logger.LogInformation(
                "Processing RefundCustomerBalanceCommand. PaymentId={PaymentId}, CustomerId={CustomerId}, Amount={Amount}, Reason={Reason}",
                message.PaymentId, message.CustomerId, message.Amount, message.Reason);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == message.CustomerId, context.CancellationToken);

            if (customer == null)
            {
                _logger.LogWarning("Customer not found for balance refund. CustomerId={CustomerId}", message.CustomerId);
                return;
            }

            int amountInt = (int)message.Amount;
            var beforeState = new { customer.SoTienTrongTaiKhoan };

            customer.RefundBalance(amountInt);
            await _context.SaveChangesAsync(context.CancellationToken);

            var afterState = new { customer.SoTienTrongTaiKhoan };

            await _audit.PublishAsync(
                "CustomerBalanceRefunded",
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                before: beforeState,
                after: afterState,
                category: Bizcore.BuildingBlocks.Audit.AuditCategory.System,
                classification: Bizcore.BuildingBlocks.Audit.DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "Customer balance refunded successfully. CustomerId={CustomerId}, AmountRefunded={Amount}, NewBalance={Balance}",
                message.CustomerId, amountInt, customer.SoTienTrongTaiKhoan);

            await context.Publish<ICustomerBalanceRefundedEvent>(new
            {
                PaymentId = message.PaymentId,
                CustomerId = message.CustomerId,
                AmountRefunded = message.Amount
            }, context.CancellationToken);
        }
    }
}
