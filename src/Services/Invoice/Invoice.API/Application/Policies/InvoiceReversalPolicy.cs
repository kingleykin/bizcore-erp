using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Reversal;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using InvoiceStatus = Bizcore.BuildingBlocks.InvoiceStatus;
using System.Security.Claims;

namespace Invoice.API.Application.Policies
{
    /// <summary>
    /// Dynamic reversal policy cho Invoice entity.
    /// Quyết định field nào được restore dựa trên:
    ///   1. Loại field (financial vs metadata)
    ///   2. Trạng thái hiện tại của Invoice
    ///   3. Quyền hạn của actor (SuperReverse cho trường hợp đặc biệt)
    /// </summary>
    public class InvoiceReversalPolicy : IReversalPolicy<InvoiceEntity>
    {
        // ── Fields tài chính — không bao giờ direct restore ───────────────────
        private static readonly HashSet<string> _financialFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "amount", "status", "duedate", "taxamount", "totalamount"
        };

        // ── Fields metadata — cho phép restore ───────────────────────────────
        private static readonly HashSet<string> _restorableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "customername", "description", "notes", "billingaddress"
        };

        // ── Fields hệ thống — không bao giờ touch ────────────────────────────
        private static readonly HashSet<string> _systemFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "id", "createdat", "rowversion"
        };

        public ReversalDecision CanRestore(string field, InvoiceEntity current, ClaimsPrincipal actor)
        {
            // Guard 1: System fields
            if (_systemFields.Contains(field))
                return ReversalDecision.Deny($"'{field}' là trường hệ thống, không thể khôi phục.");

            // Guard 2: Financial fields — phải tạo compensating entry
            if (_financialFields.Contains(field))
                return ReversalDecision.Deny(
                    $"'{field}' là trường tài chính. Cần tạo Correcting Entry thay vì restore trực tiếp.");

            // Guard 3: Invoice đã Cancelled — không ai được restore
            if (current.Status == InvoiceStatus.Cancelled)
                return ReversalDecision.Deny("Invoice đã bị hủy, không thể khôi phục bất kỳ trường nào.");

            // Guard 4: Invoice đã Paid — chỉ SuperReverse mới được phép
            if (current.Status == InvoiceStatus.Paid)
            {
                bool hasSuperReverse = actor.HasClaim("permission", Permissions.Audit.SuperReverse);
                if (!hasSuperReverse)
                    return ReversalDecision.Deny(
                        "Invoice đã thanh toán. Cần quyền 'audit:super-reverse' để khôi phục.");
            }

            // Guard 5: Field nằm trong allowlist
            if (_restorableFields.Contains(field))
                return ReversalDecision.Allow();

            return ReversalDecision.Deny($"'{field}' không nằm trong danh sách field được phép restore.");
        }
    }
}

