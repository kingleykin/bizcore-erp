using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Exceptions;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Invoice.API.Application.Commands;

public class RestoreInvoiceFieldCommandHandler : IRequestHandler<RestoreInvoiceFieldCommand, RestoreFieldResult>
{
    private readonly AppDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<RestoreInvoiceFieldCommandHandler> _logger;

    public RestoreInvoiceFieldCommandHandler(
        AppDbContext context,
        IPublishEndpoint publishEndpoint,
        IAuditPublisher audit,
        ILogger<RestoreInvoiceFieldCommandHandler> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
        _logger = logger;
    }

    public async Task<RestoreFieldResult> Handle(RestoreInvoiceFieldCommand request, CancellationToken ct)
    {
        // 1. Load entity with Version for EF Core to detect concurrent write
        var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, ct);
        if (invoice is null)
            return new RestoreFieldResult(false, $"Invoice '{request.InvoiceId}' không tồn tại.");

        // 2. Lưu snapshot trước khi restore (cho BeforeJson của AuditEvent mới)
        var beforeSnapshot = new { invoice.CustomerName, invoice.Amount, Status = invoice.Status.ToString() };

        // 3. Kiểm tra Policy (Business Authorization)
        var policy = new Invoice.API.Application.Policies.InvoiceReversalPolicy();
        var decision = policy.CanRestore(request.Field, invoice, request.Actor);
        if (!decision.IsAllowed)
            return new RestoreFieldResult(false, $"Policy Denied: {decision.Reason}");

        // 4. Gọi đúng domain method theo field (Semantic, không generic)
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
        // EF Core will throw DbUpdateConcurrencyException if Version doesn't match on commit.

        try
        {
            // 4. Publish AuditEvent ghi nhận hành động Reversal (traceability đầy đủ)
            await _audit.PublishAsync(
                AuditActions.Invoice.FieldRestored,
                entityType: "Invoice",
                entityId: request.InvoiceId.ToString(),
                before: beforeSnapshot,
                after: new { invoice.CustomerName },
                category: AuditCategory.Compliance,
                severity: AuditSeverity.Warning,
                actorUserId: request.Actor.FindFirstValue("sub"),
                actorUsername: request.Actor.FindFirstValue(ClaimTypes.Name),
                ct: ct);

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
