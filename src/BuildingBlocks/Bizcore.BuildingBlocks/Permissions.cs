namespace Bizcore.BuildingBlocks
{
    /// <summary>
    /// Centralized permission constants sử dụng convention {Resource}.{Action}.
    /// Format: PascalCase, dùng dấu chấm phân cách.
    /// Ví dụ: Invoice.View, Invoice.Amount.Edit, Menu.Invoice
    /// </summary>
    public static class Permissions
    {
        // ── Menu Navigation ──────────────────────────────────────────────────────
        public static class Menu
        {
            public const string Invoice = "Menu.Invoice";
            public const string Payment = "Menu.Payment";
            public const string Audit = "Menu.Audit";
            public const string Orchestration = "Menu.Orchestration";
            public const string Report = "Menu.Report";
            public const string Identity = "Menu.Identity";
        }

        // ── Invoice ──────────────────────────────────────────────────────────────
        public static class Invoice
        {
            public const string View = "Invoice.View";
            public const string Create = "Invoice.Create";
            public const string Update = "Invoice.Update";
            public const string Delete = "Invoice.Delete";

            // Field-level
            public const string AmountView = "Invoice.Amount.View";
            public const string AmountEdit = "Invoice.Amount.Edit";
            public const string CustomerNameEdit = "Invoice.CustomerName.Edit";
        }

        // ── Payment ──────────────────────────────────────────────────────────────
        public static class Payment
        {
            public const string View = "Payment.View";
            public const string Create = "Payment.Create";
            public const string Process = "Payment.Process";
        }

        // ── Report ───────────────────────────────────────────────────────────────
        public static class Report
        {
            public const string View = "Report.View";
            public const string Export = "Report.Export";
        }

        // ── Orchestration ─────────────────────────────────────────────────────────
        public static class Orchestration
        {
            public const string View = "Orchestration.View";
        }

        // ── Audit ────────────────────────────────────────────────────────────────
        public static class Audit
        {
            public const string View = "Audit.View";
            public const string Export = "Audit.Export";
            /// <summary>Cho phép reverse kể cả entity đang ở trạng thái đóng/đã thanh toán.</summary>
            public const string SuperReverse = "Audit.SuperReverse";
        }

        // ── Identity — Users ──────────────────────────────────────────────────────
        public static class Identity
        {
            public static class Users
            {
                public const string View = "Identity.Users.View";
                public const string Create = "Identity.Users.Create";
                public const string Update = "Identity.Users.Update";
                public const string Delete = "Identity.Users.Delete";
                public const string ManageRoles = "Identity.Users.ManageRoles";
            }

            public static class Roles
            {
                public const string View = "Identity.Roles.View";
                public const string Create = "Identity.Roles.Create";
                public const string Update = "Identity.Roles.Update";
                public const string Delete = "Identity.Roles.Delete";
                public const string ManagePermissions = "Identity.Roles.ManagePermissions";
            }
        }

        // ── Admin — Organization & System ────────────────────────────────────────
        public static class Admin
        {
            public const string OrgView = "Admin.OrgView";
            public const string SysAdmin = "Admin.SysAdmin";
            public const string SystemView = "Admin.SystemView";
        }
    }

    /// <summary>
    /// Permission scope — phân loại mục đích của permission.
    /// </summary>
    public static class PermissionScope
    {
        public const string Menu = "Menu";    // Hiển thị menu item
        public const string Page = "Page";    // Truy cập toàn bộ page
        public const string Action = "Action";  // Button/action cụ thể
        public const string Field = "Field";   // Đọc/sửa field
        public const string Data = "Data";    // Filter dữ liệu
    }
}
