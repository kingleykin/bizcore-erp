using Bizcore.BuildingBlocks;
using Identity.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Infrastructure.Data
{
    /// <summary>
    /// Seeds default data: Roles, Permissions (tất cả từ Bizcore.BuildingBlocks.Permissions),
    /// và 2 user mặc định: admin / user.
    /// Idempotent: có thể chạy lại an toàn.
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(IdentityDbContext context, ILogger logger)
        {
            await context.Database.EnsureCreatedAsync();

            // ── 1. Seed Permissions ─────────────────────────────────────────────
            var allPermissionActions = new[]
            {
                // Invoice
                Permissions.Invoice.View, Permissions.Invoice.Create,
                Permissions.Invoice.Update, Permissions.Invoice.Delete,
                // Payment
                Permissions.Payment.View, Permissions.Payment.Create, Permissions.Payment.Process,
                // Report
                Permissions.Report.View, Permissions.Report.Export,
                // Orchestration
                Permissions.Orchestration.View,
                // Identity - Users
                Permissions.Identity.Users.View, Permissions.Identity.Users.Create,
                Permissions.Identity.Users.Update, Permissions.Identity.Users.Delete,
                Permissions.Identity.Users.ManageRoles,
                // Identity - Roles
                Permissions.Identity.Roles.View, Permissions.Identity.Roles.Create,
                Permissions.Identity.Roles.Update, Permissions.Identity.Roles.Delete,
                Permissions.Identity.Roles.ManagePermissions,
            };

            var existingActions = await context.Permissions.Select(p => p.Action).ToListAsync();
            var newPermissions = allPermissionActions
                .Where(a => !existingActions.Contains(a))
                .Select(a => Permission.Create(a, $"Permission: {a}"))
                .ToList();

            if (newPermissions.Count > 0)
            {
                context.Permissions.AddRange(newPermissions);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} permissions.", newPermissions.Count);
            }

            var permissionMap = await context.Permissions.ToDictionaryAsync(p => p.Action, p => p.Id);

            // ── 2. Seed Roles ───────────────────────────────────────────────────
            if (!await context.Roles.AnyAsync(r => r.Name == "Admin"))
            {
                var adminRole = Role.Create("Admin", "Full system access", isSystem: true);
                context.Roles.Add(adminRole);
                await context.SaveChangesAsync();

                // Admin gets ALL permissions
                var adminPerms = permissionMap.Values
                    .Select(pid => new RolePermission { RoleId = adminRole.Id, PermissionId = pid });
                context.RolePermissions.AddRange(adminPerms);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded Admin role with all {Count} permissions.", permissionMap.Count);
            }

            if (!await context.Roles.AnyAsync(r => r.Name == "User"))
            {
                var userRole = Role.Create("User", "Read-only access for standard users", isSystem: true);
                context.Roles.Add(userRole);
                await context.SaveChangesAsync();

                // User gets View-only permissions
                var viewPermissions = new[]
                {
                    Permissions.Invoice.View,
                    Permissions.Report.View,
                    Permissions.Payment.View,
                };
                var userPerms = viewPermissions
                    .Where(a => permissionMap.ContainsKey(a))
                    .Select(a => new RolePermission { RoleId = userRole.Id, PermissionId = permissionMap[a] });
                context.RolePermissions.AddRange(userPerms);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded User role with view-only permissions.");
            }

            // ── 3. Seed Default Users ──────────────────────────────────────────
            var adminRoleId = (await context.Roles.FirstAsync(r => r.Name == "Admin")).Id;
            var userRoleId = (await context.Roles.FirstAsync(r => r.Name == "User")).Id;

            if (!await context.Users.AnyAsync(u => u.Username == "admin"))
            {
                var adminUser = User.Create("admin", "admin@bizcore.com",
                    BCrypt.Net.BCrypt.HashPassword("Admin@123"));
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded admin user.");
            }

            if (!await context.Users.AnyAsync(u => u.Username == "user"))
            {
                var stdUser = User.Create("user", "user@bizcore.com",
                    BCrypt.Net.BCrypt.HashPassword("User@123"));
                context.Users.Add(stdUser);
                await context.SaveChangesAsync();

                context.UserRoles.Add(new UserRole
                {
                    UserId = stdUser.Id,
                    RoleId = userRoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded standard user.");
            }
        }
    }
}
