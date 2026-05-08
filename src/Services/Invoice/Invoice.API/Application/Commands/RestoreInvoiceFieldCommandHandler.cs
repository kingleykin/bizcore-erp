using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using Invoice.API.Application.Services;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace Invoice.API.Application.Commands
{
    public class RestoreInvoiceFieldCommandHandler : IRequestHandler<RestoreInvoiceFieldCommand, RestoreFieldResult>
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<RestoreInvoiceFieldCommandHandler> _logger;

        public RestoreInvoiceFieldCommandHandler(
            AppDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<RestoreInvoiceFieldCommandHandler> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<RestoreFieldResult> Handle(RestoreInvoiceFieldCommand request, CancellationToken ct)
        {
            // 1. Load entity với RowVersion để EF Core phát hiện concurrent write
            var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, ct);
            if (invoice is null)
                return new RestoreFieldResult(false, $"Invoice '{request.InvoiceId}' không tồn tại.");

            // 2. Lưu snapshot trước khi restore (cho BeforeJson của AuditEvent mới)
            var beforeSnapshot = new { invoice.CustomerName, invoice.Amount, Status = invoice.Status.ToString() };

            // 3. Gọi đúng domain method theo field (Semantic, không generic)
            try
            {
                var normalizedField = request.Field.ToLowerInvariant();

                if (normalizedField == "customername")
                    invoice.RestoreCustomerName(request.PreviousValue);
                else
                    return new RestoreFieldResult(false,
                        $"Field '{request.Field}' chưa có domain method restore tương ứng.");
            }
            catch (DomainException ex)
            {
                return new RestoreFieldResult(false, ex.Message);
            }

            // Transaction is managed by TransactionBehavior pipeline.
            // EF Core will throw DbUpdateConcurrencyException if RowVersion doesn't match on commit.

            try
            {
                // 4. Publish AuditEvent ghi nhận hành động Reversal (traceability đầy đủ)
                // Event này sẽ được ghi vào MassTransit Outbox cùng transaction của DbContext
                var activity = Activity.Current;
                await _publishEndpoint.Publish(new AuditEvent
                {
                    ServiceName    = "Invoice.API",
                    Action         = $"DataReversal.Invoice.{request.Field}",
                    AuditLevel     = "Compliance",
                    EntityType     = "Invoice",
                    EntityId       = request.InvoiceId.ToString(),
                    BeforeJson     = SensitiveFieldMasker.ToMaskedJson(beforeSnapshot),
                    AfterJson      = SensitiveFieldMasker.ToMaskedJson(new { invoice.CustomerName }),
                    ActorUserId    = request.Actor.FindFirstValue("sub"),
                    ActorUsername  = request.Actor.FindFirstValue(System.Security.Claims.ClaimTypes.Name),
                    CorrelationId  = $"reversal-of-{request.SourceAuditEntryId}",
                    TraceId        = activity?.TraceId.ToString(),
                    SpanId         = activity?.SpanId.ToString(),
                    OccurredAt     = DateTime.UtcNow
                }, ct);

                _logger.LogInformation("Invoice field '{Field}' restoration prepared for InvoiceId={InvoiceId}", request.Field, request.InvoiceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing restore event for InvoiceId={InvoiceId}", request.InvoiceId);
                throw;
            }

            return new RestoreFieldResult(true,
                $"Field '{request.Field}' đã được khôi phục thành công về giá trị: '{request.PreviousValue}'.");
        }
    }
}
