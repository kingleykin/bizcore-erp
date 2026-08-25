using Bizcore.BuildingBlocks;
using Admin.API.Domain.Entities;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Infrastructure.Data
{
    /// <summary>
    /// Seeds: Permissions (full metadata + Menu permissions), Roles (Admin/Accountant/Viewer),
    /// NavigationMenus, và 2 default users.
    /// Idempotent — an toàn khi chạy lại.
    /// </summary>
    public static class DbSeeder
    {
        // ── Permission definitions ───────────────────────────────────────────────
        private record PermDef(string Code, string Name, string Resource, string Scope, string? Desc = null);

        private static readonly PermDef[] AllPermissions =
        [
            // Menu navigation
            new(Permissions.Menu.Invoice,      "Xem menu Invoice",      "Navigation.Invoice",      PermissionScope.Menu),
            new(Permissions.Menu.Payment,      "Xem menu Payment",      "Navigation.Payment",      PermissionScope.Menu),
            new(Permissions.Menu.Audit,        "Xem menu Audit",        "Navigation.Audit",        PermissionScope.Menu),
            new(Permissions.Menu.Orchestration,"Xem menu Orchestration","Navigation.Orchestration",PermissionScope.Menu),
            new(Permissions.Menu.Report,       "Xem menu Report",       "Navigation.Report",       PermissionScope.Menu),
            new(Permissions.Menu.Identity,     "Xem menu Admin",        "Navigation.Admin",        PermissionScope.Menu),
            new(Permissions.Menu.Customer,     "Xem menu Customer",     "Navigation.Customer",     PermissionScope.Menu),
            new(Permissions.Menu.Order,        "Xem menu Order",        "Navigation.Order",        PermissionScope.Menu),
            new(Permissions.Menu.Product,      "Xem menu Product",      "Navigation.Product",      PermissionScope.Menu),

            // Admin Service permissions
            new(Permissions.Admin.OrgView,     "Xem cấu trúc tổ chức",  "Admin.Org",     PermissionScope.Page),
            new(Permissions.Admin.SysAdmin,    "Quản trị hệ thống",     "Admin.System",  PermissionScope.Page),
            new(Permissions.Admin.SystemView,  "Xem cấu hình hệ thống", "Admin.System",  PermissionScope.Page),

            // Invoice — page & action
            new(Permissions.Invoice.View,   "Xem danh sách Invoice",   "Invoice", PermissionScope.Page),
            new(Permissions.Invoice.Create, "Tạo Invoice mới",         "Invoice", PermissionScope.Action),
            new(Permissions.Invoice.Update, "Cập nhật Invoice",        "Invoice", PermissionScope.Action),
            new(Permissions.Invoice.Delete, "Xóa Invoice",             "Invoice", PermissionScope.Action),

            // Invoice — field level
            new(Permissions.Invoice.AmountView,      "Xem trường Amount",       "Invoice.Amount",       PermissionScope.Field),
            new(Permissions.Invoice.AmountEdit,      "Sửa trường Amount",       "Invoice.Amount",       PermissionScope.Field),
            new(Permissions.Invoice.CustomerNameEdit,"Sửa tên khách hàng",      "Invoice.CustomerName", PermissionScope.Field),

            // Payment
            new(Permissions.Payment.View,    "Xem danh sách Giao dịch (Payment)", "Payment", PermissionScope.Page),
            new(Permissions.Payment.Create,  "Thực hiện thanh toán (Pay)",         "Payment", PermissionScope.Action),
            new(Permissions.Payment.Process, "Xử lý/Approve Giao dịch",           "Payment", PermissionScope.Action),

            // Report
            new(Permissions.Report.View,   "Xem báo cáo",    "Report", PermissionScope.Page),
            new(Permissions.Report.Export, "Xuất báo cáo",   "Report", PermissionScope.Action),

            // Orchestration
            new(Permissions.Orchestration.View, "Xem Orchestration flows", "Orchestration", PermissionScope.Page),

            // Audit
            new(Permissions.Audit.View,         "Xem Audit log",               "Audit", PermissionScope.Page),
            new(Permissions.Audit.Export,        "Xuất Audit log",              "Audit", PermissionScope.Action),
            new(Permissions.Audit.SuperReverse,  "Super Reverse (override)",    "Audit", PermissionScope.Action,
                "Cho phép đảo ngược kể cả entity đã đóng/thanh toán"),

            // Identity — Users
            new(Permissions.Identity.Users.View,        "Xem danh sách Users",          "Identity.Users", PermissionScope.Page),
            new(Permissions.Identity.Users.Create,      "Tạo User mới",                 "Identity.Users", PermissionScope.Action),
            new(Permissions.Identity.Users.Update,      "Cập nhật User",                "Identity.Users", PermissionScope.Action),
            new(Permissions.Identity.Users.Delete,      "Xóa User",                     "Identity.Users", PermissionScope.Action),
            new(Permissions.Identity.Users.ManageRoles, "Gán/Thu hồi Role cho User",    "Identity.Users", PermissionScope.Action),

            // Identity — Roles
            new(Permissions.Identity.Roles.View,              "Xem danh sách Roles",          "Identity.Roles", PermissionScope.Page),
            new(Permissions.Identity.Roles.Create,            "Tạo Role mới",                 "Identity.Roles", PermissionScope.Action),
            new(Permissions.Identity.Roles.Update,            "Cập nhật Role",                "Identity.Roles", PermissionScope.Action),
            new(Permissions.Identity.Roles.Delete,            "Xóa Role",                     "Identity.Roles", PermissionScope.Action),
            new(Permissions.Identity.Roles.ManagePermissions, "Gán/Thu hồi Permission cho Role","Identity.Roles", PermissionScope.Action),

            // Customer
            new(Permissions.Customer.View,   "Xem danh sách Khách hàng", "Customer", PermissionScope.Page),
            new(Permissions.Customer.Create, "Tạo Khách hàng mới",       "Customer", PermissionScope.Action),
            new(Permissions.Customer.Update, "Cập nhật Khách hàng",      "Customer", PermissionScope.Action),
            new(Permissions.Customer.Delete, "Xóa Khách hàng",           "Customer", PermissionScope.Action),

            // CustomerGroup
            new(Permissions.CustomerGroup.View,   "Xem danh sách Nhóm khách hàng", "Customer.Group", PermissionScope.Page),
            new(Permissions.CustomerGroup.Create, "Tạo Nhóm khách hàng mới",       "Customer.Group", PermissionScope.Action),
            new(Permissions.CustomerGroup.Update, "Cập nhật Nhóm khách hàng",      "Customer.Group", PermissionScope.Action),
            new(Permissions.CustomerGroup.Delete, "Xóa Nhóm khách hàng",           "Customer.Group", PermissionScope.Action),

            // Order
            new(Permissions.Order.View,   "Xem danh sách Đơn hàng", "Order", PermissionScope.Page),
            new(Permissions.Order.Create, "Tạo Đơn hàng mới",       "Order", PermissionScope.Action),
            new(Permissions.Order.Update, "Cập nhật/Xác nhận Đơn hàng", "Order", PermissionScope.Action),
            new(Permissions.Order.Cancel, "Hủy Đơn hàng",           "Order", PermissionScope.Action),

            // Product
            new(Permissions.Product.View,   "Xem danh sách Sản phẩm", "Product", PermissionScope.Page),
            new(Permissions.Product.Create, "Tạo Sản phẩm mới",       "Product", PermissionScope.Action),
            new(Permissions.Product.Update, "Cập nhật Sản phẩm",      "Product", PermissionScope.Action),
            new(Permissions.Product.Delete, "Xóa Sản phẩm",           "Product", PermissionScope.Action),
        ];

        // ── Navigation menu definitions ─────────────────────────────────────────
        private record NavDef(string Name, string Route, string PermCode, int Sort, string? Icon = null);

        private static readonly NavDef[] AllMenus =
        [
            new("Invoice",       "/invoice",       Permissions.Menu.Invoice,       10, "receipt"),
            new("Payment",       "/payment",       Permissions.Menu.Payment,       20, "credit-card"),
            new("Report",        "/report",        Permissions.Menu.Report,        30, "bar-chart"),
            new("Orchestration", "/orchestration", Permissions.Menu.Orchestration, 40, "git-branch"),
            new("Audit",         "/audit",         Permissions.Menu.Audit,         50, "shield-check"),
            new("Admin",         "/admin",         Permissions.Menu.Identity,      60, "settings"),
            new("Customer",      "/customer",      Permissions.Menu.Customer,      15, "users"),
            new("Order",         "/orders",        Permissions.Menu.Order,         16, "shopping-cart"),
            new("Product",       "/products",      Permissions.Menu.Product,       17, "package"),
        ];

        public static async Task SeedAsync(AdminDbContext context, ILogger logger)
        {
            // ── 1. Seed Permissions ────────────────────────────────────────────
            var existingCodes = await context.Permissions.Select(p => p.Code).ToListAsync();
            var newPerms = AllPermissions
                .Where(d => !existingCodes.Contains(d.Code))
                .Select(d => Permission.Create(d.Code, d.Name, d.Resource, d.Scope, d.Desc))
                .ToList();

            if (newPerms.Count > 0)
            {
                context.Permissions.AddRange(newPerms);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} permissions.", newPerms.Count);
            }

            var permMap = await context.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);

            // ── 2. Seed NavigationMenus ────────────────────────────────────────
            var existingRoutes = await context.NavigationMenus.Select(n => n.Route).ToListAsync();
            var newMenus = AllMenus
                .Where(d => !existingRoutes.Contains(d.Route))
                .Select(d => NavigationMenu.Create(d.Name, d.Route, d.PermCode, d.Sort, d.Icon))
                .ToList();

            if (newMenus.Count > 0)
            {
                context.NavigationMenus.AddRange(newMenus);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} navigation menu items.", newMenus.Count);
            }

            // ── 3. Seed Currencies ─────────────────────────────────────────────
            if (!await context.Currencies.AnyAsync())
            {
                context.Currencies.AddRange(
                    Currency.Create("VND", "Vietnamese Dong", "₫", 0),
                    Currency.Create("USD", "US Dollar", "$", 2),
                    Currency.Create("EUR", "Euro", "€", 2)
                );
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded basic currencies.");
            }

            // ── 4. Seed LegalEntity ───────────────────────────────────────────
            if (!await context.LegalEntities.AnyAsync())
            {
                var bizcore = LegalEntity.Create("BIZCORE-VN", "Công ty Cổ phần Bizcore Việt Nam",
                    taxCode: "0101234567", baseCurrencyCode: "VND");
                context.LegalEntities.Add(bizcore);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded default LegalEntity: BIZCORE-VN");
            }

            // ── 5. Seed Roles ──────────────────────────────────────────────────
            await SeedRoleAsync(context, logger, permMap, "Admin",
                description: "Toàn quyền hệ thống",
                permCodes: AllPermissions.Select(p => p.Code).ToArray());

            await SeedRoleAsync(context, logger, permMap, "Accountant",
                description: "Kế toán — xem và xử lý Invoice/Payment",
                permCodes:
                [
                    Permissions.Menu.Invoice, Permissions.Menu.Payment, Permissions.Menu.Report, Permissions.Menu.Customer,
                    Permissions.Invoice.View, Permissions.Invoice.Create, Permissions.Invoice.Update,
                    Permissions.Invoice.AmountView,
                    Permissions.Payment.View, Permissions.Payment.Create, Permissions.Payment.Process,
                    Permissions.Report.View,
                    Permissions.Admin.OrgView,
                    Permissions.Customer.View, Permissions.Customer.Create, Permissions.Customer.Update,
                    Permissions.CustomerGroup.View,
                ]);

            await SeedRoleAsync(context, logger, permMap, "Viewer",
                description: "Chỉ xem — không thao tác",
                permCodes:
                [
                    Permissions.Menu.Invoice, Permissions.Menu.Payment, Permissions.Menu.Report, Permissions.Menu.Customer,
                    Permissions.Invoice.View, Permissions.Invoice.AmountView,
                    Permissions.Payment.View,
                    Permissions.Report.View,
                    Permissions.Admin.OrgView,
                    Permissions.Customer.View,
                    Permissions.CustomerGroup.View,
                ]);

            // ── 6. Seed Default Users ──────────────────────────────────────────
            await SeedUserAsync(context, logger, "admin", "admin@bizcore.com", "Admin@123", "Admin");
            await SeedUserAsync(context, logger, "accountant", "accountant@bizcore.com", "Acc@123", "Accountant");
            await SeedUserAsync(context, logger, "viewer", "viewer@bizcore.com", "Viewer@123", "Viewer");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static async Task SeedRoleAsync(
            AdminDbContext context,
            ILogger logger,
            Dictionary<string, Guid> permMap,
            string roleName,
            string description,
            string[] permCodes)
        {
            var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                role = Role.Create(roleName, description, isSystem: true);
                context.Roles.Add(role);
                await context.SaveChangesAsync();
            }

            // Sync permissions (Add new ones)
            var existingPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var newRolePerms = permCodes
                .Where(c => permMap.ContainsKey(c))
                .Select(c => permMap[c])
                .Where(id => !existingPermIds.Contains(id))
                .Select(id => new RolePermission { RoleId = role.Id, PermissionId = id });

            if (newRolePerms.Any())
            {
                context.RolePermissions.AddRange(newRolePerms);
                await context.SaveChangesAsync();
                logger.LogInformation("Synced permissions for role '{Role}'.", roleName);
            }
        }

        private static async Task SeedUserAsync(
            AdminDbContext context,
            ILogger logger,
            string username,
            string email,
            string password,
            string roleName)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                user = User.Create(username, email, BCrypt.Net.BCrypt.HashPassword(password));
                context.Users.Add(user);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded user '{Username}'.", username);
            }
            else
            {
                user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(password));
                context.Users.Update(user);
                await context.SaveChangesAsync();
                logger.LogInformation("Updated password for user '{Username}'.", username);
            }

            var roleId = (await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName))?.Id;
            if (roleId.HasValue)
            {
                var userHasRole = await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId.Value);
                if (!userHasRole)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = roleId.Value,
                        AssignedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                    logger.LogInformation("Assigned role '{Role}' to user '{Username}'.", roleName, username);
                }
            }
        }
    }
}
