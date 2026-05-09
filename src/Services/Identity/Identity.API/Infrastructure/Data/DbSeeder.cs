using Bizcore.BuildingBlocks;
using Identity.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Infrastructure.Data
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
            new(Permissions.Menu.Identity,     "Xem menu Identity",     "Navigation.Identity",     PermissionScope.Menu),

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
            new(Permissions.Payment.View,    "Xem danh sách Payment",  "Payment", PermissionScope.Page),
            new(Permissions.Payment.Create,  "Tạo Payment",            "Payment", PermissionScope.Action),
            new(Permissions.Payment.Process, "Xử lý/Approve Payment",  "Payment", PermissionScope.Action),

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
            new("Identity",      "/identity",      Permissions.Menu.Identity,      60, "users"),
        ];

        public static async Task SeedAsync(IdentityDbContext context, ILogger logger)
        {
            // Migration is already handled in Program.cs via DatabaseExtensions.MigrateDatabaseAsync

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

            // ── 3. Seed Roles ──────────────────────────────────────────────────
            await SeedRoleAsync(context, logger, permMap, "Admin",
                description: "Toàn quyền hệ thống",
                permCodes: AllPermissions.Select(p => p.Code).ToArray());

            await SeedRoleAsync(context, logger, permMap, "Accountant",
                description: "Kế toán — xem và xử lý Invoice/Payment",
                permCodes:
                [
                    Permissions.Menu.Invoice, Permissions.Menu.Payment, Permissions.Menu.Report,
                    Permissions.Invoice.View, Permissions.Invoice.Create, Permissions.Invoice.Update,
                    Permissions.Invoice.AmountView,
                    Permissions.Payment.View, Permissions.Payment.Create, Permissions.Payment.Process,
                    Permissions.Report.View,
                ]);

            await SeedRoleAsync(context, logger, permMap, "Viewer",
                description: "Chỉ xem — không thao tác",
                permCodes:
                [
                    Permissions.Menu.Invoice, Permissions.Menu.Payment, Permissions.Menu.Report,
                    Permissions.Invoice.View, Permissions.Invoice.AmountView,
                    Permissions.Payment.View,
                    Permissions.Report.View,
                ]);

            // ── 4. Seed Default Users ──────────────────────────────────────────
            await SeedUserAsync(context, logger, "admin",   "admin@bizcore.com",   "Admin@123",   "Admin");
            await SeedUserAsync(context, logger, "accountant", "accountant@bizcore.com", "Acc@123", "Accountant");
            await SeedUserAsync(context, logger, "viewer",  "viewer@bizcore.com",  "Viewer@123",  "Viewer");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static async Task SeedRoleAsync(
            IdentityDbContext context,
            ILogger logger,
            Dictionary<string, Guid> permMap,
            string roleName,
            string description,
            string[] permCodes)
        {
            if (await context.Roles.AnyAsync(r => r.Name == roleName)) return;

            var role = Role.Create(roleName, description, isSystem: true);
            context.Roles.Add(role);
            await context.SaveChangesAsync();

            var rolePerms = permCodes
                .Where(c => permMap.ContainsKey(c))
                .Select(c => new RolePermission { RoleId = role.Id, PermissionId = permMap[c] });

            context.RolePermissions.AddRange(rolePerms);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded role '{Role}' with {Count} permissions.", roleName, permCodes.Length);
        }

        private static async Task SeedUserAsync(
            IdentityDbContext context,
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
                // Update password if it was dummy or changed (self-healing)
                // Note: In production you wouldn't override passwords on every seed, 
                // but this ensures the dev environment has the correct password.
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
                        UserId     = user.Id,
                        RoleId     = roleId.Value,
                        AssignedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                    logger.LogInformation("Assigned role '{Role}' to user '{Username}'.", roleName, username);
                }
            }
        }
    }
}
