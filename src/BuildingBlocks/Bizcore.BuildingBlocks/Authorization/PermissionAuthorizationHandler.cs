using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Linq;

namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Authorization handler kiểm tra permission của user từ claims "permission".
    /// Mỗi microservice đăng ký handler này; permission list được load từ JWT claims
    /// (trong chế độ JWT-claims) hoặc từ PermissionCache (nếu dùng dynamic lookup).
    /// 
    /// Hiện tại: đọc claim "permission" từ JWT (generated bởi Admin.API).
    /// Tương lai: swap sang IPermissionCache khi các service chuyển sang cache lookup.
    /// </summary>
    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ILogger<PermissionAuthorizationHandler> _logger;
        private readonly IPermissionCache? _cache;

        public PermissionAuthorizationHandler(
            ILogger<PermissionAuthorizationHandler> logger,
            IPermissionCache? cache = null)
        {
            _logger = logger;
            _cache = cache;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // 1. Kiểm tra trong cache (Source of Truth cho runtime changes)
            if (_cache != null)
            {
                var userIdStr = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) 
                             ?? context.User.FindFirstValue("sub");
                
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    var cachedPermissions = await _cache.GetAsync(userId);
                    if (cachedPermissions != null)
                    {
                        if (cachedPermissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
                        {
                            context.Succeed(requirement);
                            _logger.LogDebug("Permission '{Permission}' granted from cache to user '{UserId}'.", requirement.Permission, userId);
                            return;
                        }
                    }
                }
            }

            // 2. Fallback: Lấy từ JWT claims (Nếu cache miss hoặc cache không khả dụng)
            var userPermissions = context.User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (userPermissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                _logger.LogDebug(
                    "Permission '{Permission}' granted from JWT to user '{User}'.",
                    requirement.Permission,
                    context.User.Identity?.Name ?? "unknown");
            }
            else
            {
                _logger.LogDebug(
                    "Permission '{Permission}' denied for user '{User}'.",
                    requirement.Permission,
                    context.User.Identity?.Name ?? "unknown");
            }
        }
    }
}
