using Asp.Versioning;
using Bizcore.BuildingBlocks.Authorization;
using Identity.API.Application.DTOs;
using Identity.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Identity.API.Controllers
{
    /// <summary>
    /// Endpoints phục vụ thông tin của user hiện tại (current user context).
    /// Frontend gọi ngay sau login để lấy permissions và render menu động.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/me")]
    [ApiVersion("1.0")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly IdentityDbContext     _db;
        private readonly IPermissionCache?     _cache;
        private readonly ILogger<MeController> _logger;

        public MeController(
            IdentityDbContext     db,
            ILogger<MeController> logger,
            IPermissionCache?     cache = null)
        {
            _db     = db;
            _logger = logger;
            _cache  = cache;
        }

        /// <summary>
        /// Trả về danh sách permission codes của user hiện tại.
        /// Frontend dùng để kiểm soát button/action visibility.
        /// </summary>
        [HttpGet("permissions")]
        [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // 1. Thử lấy từ cache
            var cached = await (_cache?.GetAsync(userId.Value, ct) ?? Task.FromResult<string[]?>(null));
            if (cached != null)
            {
                var username = User.FindFirstValue(ClaimTypes.Name)
                            ?? User.FindFirstValue("unique_name") ?? "unknown";
                var rolesFromJwt = User.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToArray();

                return Ok(new UserPermissionsDto(userId.Value, username, rolesFromJwt, cached));
            }

            // 2. Fallback: load từ DB
            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

            if (user == null)
                return Unauthorized();

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            // 3. Lưu vào cache
            if (_cache != null)
            {
                await _cache.SetAsync(userId.Value, permissions, ct);

                // Track users in roles for role-based invalidation
                foreach (var role in user.UserRoles)
                {
                    await _cache.TrackUserInRoleAsync(userId.Value, role.RoleId);
                }
            }

            _logger.LogDebug("Loaded {Count} permissions for user {UserId} from DB.", permissions.Length, userId);

            return Ok(new UserPermissionsDto(userId.Value, user.Username, roles, permissions));
        }

        /// <summary>
        /// Trả về danh sách menu items mà user hiện tại có quyền truy cập.
        /// Frontend render menu động từ response này, không hardcode.
        /// </summary>
        [HttpGet("navigation")]
        [ProducesResponseType(typeof(NavigationMenuDto[]), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyNavigation(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // Lấy permissions của user
            string[] userPermissions;

            var cached = await (_cache?.GetAsync(userId.Value, ct) ?? Task.FromResult<string[]?>(null));
            if (cached != null)
            {
                userPermissions = cached;
            }
            else
            {
                var user = await _db.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.RolePermissions)
                                .ThenInclude(rp => rp.Permission)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

                if (user == null) return Unauthorized();

                userPermissions = user.UserRoles
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Select(rp => rp.Permission.Code)
                    .Distinct()
                    .ToArray();

                if (_cache != null)
                {
                    await _cache.SetAsync(userId.Value, userPermissions, ct);
                    
                    foreach (var role in user.UserRoles)
                    {
                        await _cache.TrackUserInRoleAsync(userId.Value, role.RoleId);
                    }
                }
            }

            // Lọc menu items theo permission của user
            var permissionSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);

            var menus = await _db.NavigationMenus
                .Where(m => m.IsActive && permissionSet.Contains(m.PermissionCode))
                .OrderBy(m => m.SortOrder)
                .AsNoTracking()
                .ToListAsync(ct);

            var result = menus.Select(m => new NavigationMenuDto(
                m.Id, m.ParentId, m.Name, m.Route, m.Icon, m.SortOrder
            )).ToArray();

            return Ok(result);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
