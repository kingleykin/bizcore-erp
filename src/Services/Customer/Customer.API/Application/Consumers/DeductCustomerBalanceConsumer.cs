using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IDeductCustomerBalanceCommand từ Saga.
    /// Kiểm tra số dư và trừ tiền tài khoản khách hàng.
    /// </summary>
    public class DeductCustomerBalanceConsumer : IConsumer<IDeductCustomerBalanceCommand>
    {
        private readonly CustomerDbContext _context;
        private readonly Bizcore.BuildingBlocks.Audit.IAuditPublisher _audit;
        private readonly ILogger<DeductCustomerBalanceConsumer> _logger;

        public DeductCustomerBalanceConsumer(CustomerDbContext context, Bizcore.BuildingBlocks.Audit.IAuditPublisher audit, ILogger<DeductCustomerBalanceConsumer> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IDeductCustomerBalanceCommand> context)
        {
            var message = context.Message;
            _logger.LogInformation(
                "Processing DeductCustomerBalanceCommand. PaymentId={PaymentId}, CustomerId={CustomerId}, Amount={Amount}",
                message.PaymentId, message.CustomerId, message.Amount);

            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == message.CustomerId, context.CancellationToken);

                if (customer == null)
                {
                    _logger.LogWarning("Customer not found. CustomerId={CustomerId}", message.CustomerId);
                    await context.Publish<ICustomerBalanceDeductionFailedEvent>(new
                    {
                        PaymentId = message.PaymentId,
                        CustomerId = message.CustomerId,
                        Reason = "Không tìm thấy khách hàng."
                    }, context.CancellationToken);
                    return;
                }

                int amountInt = (int)message.Amount;
                var beforeState = new { customer.SoTienTrongTaiKhoan };

                // This will throw InvalidOperationException if insufficient balance
                customer.DeductBalance(amountInt);
                await _context.SaveChangesAsync(context.CancellationToken);

                var afterState = new { customer.SoTienTrongTaiKhoan };

                await _audit.PublishAsync(
                    "CustomerBalanceDeducted",
                    entityType: "Customer",
                    entityId: customer.Id.ToString(),
                    before: beforeState,
                    after: afterState,
                    category: Bizcore.BuildingBlocks.Audit.AuditCategory.System,
                    classification: Bizcore.BuildingBlocks.Audit.DataClassification.Financial,
                    ct: context.CancellationToken);

                _logger.LogInformation(
                    "Customer balance deducted successfully. CustomerId={CustomerId}, AmountDeducted={Amount}, RemainingBalance={Balance}",
                    message.CustomerId, amountInt, customer.SoTienTrongTaiKhoan);

                await context.Publish<ICustomerBalanceDeductedEvent>(new
                {
                    PaymentId = message.PaymentId,
                    CustomerId = message.CustomerId,
                    AmountDeducted = message.Amount
                }, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Customer balance deduction failed. PaymentId={PaymentId}, CustomerId={CustomerId}, Reason={Reason}",
                    message.PaymentId, message.CustomerId, ex.Message);

                await context.Publish<ICustomerBalanceDeductionFailedEvent>(new
                {
                    PaymentId = message.PaymentId,
                    CustomerId = message.CustomerId,
                    Reason = ex.Message
                }, context.CancellationToken);
            }
        }
    }
}
