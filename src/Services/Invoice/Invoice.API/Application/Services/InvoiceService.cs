using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using Invoice.API.Application.Clients;
using Invoice.API.Domain.Entities;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace Invoice.API.Application.Services
{
    public interface IInvoiceService
    {
        Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync();
        Task<Invoice.API.Domain.Entities.Invoice?>             GetByIdAsync(Guid id);
        Task<Invoice.API.Domain.Entities.Invoice>              CreateAsync(Invoice.API.Domain.Entities.Invoice invoice);
        Task<bool>                                             UpdateStatusAsync(Guid id, InvoiceStatus status);

        /// <summary>
        /// Khôi phục một field cụ thể về giá trị cũ từ AuditEntry.
        /// Chỉ áp dụng cho non-financial fields đã được InvoiceReversalPolicy cho phép.
        /// </summary>
        Task<RestoreFieldResult> RestoreFieldAsync(
            Guid   invoiceId,
            string field,
            string previousValue,
            Guid   sourceAuditEntryId,
            string reason,
            ClaimsPrincipal actor,
            CancellationToken ct = default);
    }

    public record RestoreFieldResult(bool Success, string Message, Guid? NewAuditEntryId = null);

    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext     _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public InvoiceService(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context         = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<IEnumerable<Invoice.API.Domain.Entities.Invoice>> GetAllAsync()
            => await _context.Invoices.ToListAsync();

        public async Task<Invoice.API.Domain.Entities.Invoice?> GetByIdAsync(Guid id)
            => await _context.Invoices.FindAsync(id);

        public async Task<Invoice.API.Domain.Entities.Invoice> CreateAsync(Invoice.API.Domain.Entities.Invoice invoice)
        {
            _context.Invoices.Add(invoice);
            await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
            {
                invoice.Id,
                invoice.CustomerName,
                invoice.Amount,
                invoice.CreatedAt
            });
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<bool> UpdateStatusAsync(Guid id, InvoiceStatus status)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice is null) return false;
            invoice.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<RestoreFieldResult> RestoreFieldAsync(
            Guid   invoiceId,
            string field,
            string previousValue,
            Guid   sourceAuditEntryId,
            string reason,
            ClaimsPrincipal actor,
            CancellationToken ct = default)
        {
            // 1. Load entity với RowVersion để EF Core phát hiện concurrent write
            var invoice = await _context.Invoices.FindAsync(new object[] { invoiceId }, ct);
            if (invoice is null)
                return new RestoreFieldResult(false, $"Invoice '{invoiceId}' không tồn tại.");

            // 2. Lưu snapshot trước khi restore (cho BeforeJson của AuditEvent mới)
            var beforeSnapshot = new { invoice.CustomerName, invoice.Amount, Status = invoice.Status.ToString() };

            // 3. Gọi đúng domain method theo field (Semantic, không generic)
            try
            {
                var normalizedField = field.ToLowerInvariant();

                if (normalizedField == "customername")
                    invoice.RestoreCustomerName(previousValue);
                else
                    return new RestoreFieldResult(false,
                        $"Field '{field}' chưa có domain method restore tương ứng.");
            }
            catch (DomainException ex)
            {
                return new RestoreFieldResult(false, ex.Message);
            }

            // 4. Save — EF Core sẽ throw DbUpdateConcurrencyException nếu RowVersion không khớp
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new RestoreFieldResult(false,
                    "Invoice đã bị thay đổi bởi người khác trong lúc này. Vui lòng tải lại và thử lại.");
            }

            // 5. Publish AuditEvent ghi nhận hành động Reversal (traceability đầy đủ)
            var activity = Activity.Current;
            await _publishEndpoint.Publish(new AuditEvent
            {
                ServiceName    = "Invoice.API",
                Action         = $"DataReversal.Invoice.{field}",
                AuditLevel     = "Compliance",
                EntityType     = "Invoice",
                EntityId       = invoiceId.ToString(),
                BeforeJson     = SensitiveFieldMasker.ToMaskedJson(beforeSnapshot),
                AfterJson      = SensitiveFieldMasker.ToMaskedJson(new { invoice.CustomerName }),
                ActorUserId    = actor.FindFirstValue("sub"),
                ActorUsername  = actor.FindFirstValue(System.Security.Claims.ClaimTypes.Name),
                CorrelationId  = $"reversal-of-{sourceAuditEntryId}",
                TraceId        = activity?.TraceId.ToString(),
                SpanId         = activity?.SpanId.ToString(),
                OccurredAt     = DateTime.UtcNow
            }, ct);

            return new RestoreFieldResult(true,
                $"Field '{field}' đã được khôi phục thành công về giá trị: '{previousValue}'.");
        }
    }
}

