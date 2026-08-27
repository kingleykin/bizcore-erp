using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Application.Consumers
{
    /// <summary>
    /// Consumer nhận OrderConfirmedEvent từ Order.API — tự động sinh Invoice (chứng từ/biên lai)
    /// phái sinh từ Order vừa Confirm (đã thanh toán qua saga Payment-Order). Đây là nguồn tạo
    /// Invoice DUY NHẤT hiện nay — API tạo hóa đơn thủ công đã bị gỡ khỏi InvoicesController.
    ///
    /// KHÔNG đi qua MediatR/CreateInvoiceCommand (ITransactionalCommand) — MassTransit đã tự bọc
    /// Consume() trong 1 transaction sẵn (Transactional Inbox), mở thêm 1 transaction nữa qua
    /// TransactionBehavior sẽ throw "connection already in a transaction" (bug thật đã gặp ở
    /// PaymentConfirmedConsumer/Order.API và 3 consumer bên Orchestration.API). Thao tác thẳng
    /// AppDbContext, tự SaveChangesAsync.
    /// </summary>
    public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IAuditPublisher _audit;
        private readonly ILogger<OrderConfirmedConsumer> _logger;

        public OrderConfirmedConsumer(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            IAuditPublisher audit,
            ILogger<OrderConfirmedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _audit = audit;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
        {
            var msg = context.Message;

            var alreadyExists = await _context.Invoices.AnyAsync(i => i.OrderId == msg.Id, context.CancellationToken);
            if (alreadyExists)
            {
                _logger.LogInformation("[Invoice] Invoice for OrderId={OrderId} already exists. Skipping.", msg.Id);
                return;
            }

            var invoice = Domain.Entities.Invoice.CreateFromOrder(msg.Id, msg.CustomerName, msg.TotalAmount);
            _context.Invoices.Add(invoice);

            await _context.SaveChangesAsync(context.CancellationToken);

            await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
            {
                invoice.Id,
                invoice.CustomerName,
                invoice.Amount,
                invoice.CreatedAt
            }, context.CancellationToken);

            await _audit.PublishAsync(
                AuditActions.Invoice.Created,
                entityType: "Invoice",
                entityId: invoice.Id.ToString(),
                after: new { invoice.Id, invoice.OrderId, invoice.CustomerName, invoice.Amount, invoice.Status },
                category: AuditCategory.Financial,
                classification: DataClassification.Financial,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "[Invoice] Auto-generated InvoiceId={InvoiceId} from OrderId={OrderId}", invoice.Id, msg.Id);
        }
    }
}
