using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận IPaymentCompensationRequestedEvent — publish khi thanh toán một Order đã
    /// Completed nhưng bước sau đó (Order.Confirm hoặc Inventory.Commit) thất bại. Hủy
    /// (InvoiceStatus.Cancelled) hóa đơn được OrderConfirmedConsumer tự tạo (Paid) từ cùng đơn
    /// hàng, để khớp lại với việc Payment.API đã Reverse payment tương ứng.
    ///
    /// Chỉ xử lý khi OrderId có giá trị — thanh toán Hóa đơn trực tiếp (InvoiceId) không rơi vào
    /// tình huống này vì hóa đơn đó được Mark Paid ngay trong bước validate của Payment, trước khi
    /// Payment.Status chuyển Completed.
    /// </summary>
    public class PaymentCompensationRequestedConsumer : IConsumer<IPaymentCompensationRequestedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<PaymentCompensationRequestedConsumer> _logger;

        public PaymentCompensationRequestedConsumer(
            AppDbContext context,
            IAuditPublisher audit,
            ILogger<PaymentCompensationRequestedConsumer> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IPaymentCompensationRequestedEvent> context)
        {
            var msg = context.Message;
            if (msg.OrderId is not { } orderId)
                return;

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId, context.CancellationToken);
            if (invoice == null)
            {
                // Invoice.API's OrderConfirmedConsumer (chạy song song, cùng lắng nghe
                // OrderConfirmedEvent) có thể chưa xử lý xong — throw để MassTransit retry.
                _logger.LogWarning(
                    "[Invoice] No invoice yet for OrderId={OrderId} when processing payment compensation, will retry",
                    orderId);
                throw new InvalidOperationException($"Invoice for OrderId={orderId} not found yet.");
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                _logger.LogInformation("[Invoice] InvoiceId={InvoiceId} already cancelled, skip (idempotent).", invoice.Id);
                return;
            }

            invoice.UpdateStatus(InvoiceStatus.Cancelled);
            await _context.SaveChangesAsync(context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Invoice.StatusUpdated,
                entityType: "Invoice",
                entityId: invoice.Id.ToString(),
                after: new { invoice.Id, invoice.Status },
                category: AuditCategory.Business,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogWarning(
                "[Invoice] Cancelled InvoiceId={InvoiceId} for OrderId={OrderId} due to payment compensation: {Reason}",
                invoice.Id, orderId, msg.Reason);
        }
    }
}
