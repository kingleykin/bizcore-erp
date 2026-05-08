using Microsoft.AspNetCore.Authorization;

namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Yêu cầu authorization với một permission code cụ thể.
    /// Được dùng bởi PermissionAuthorizationHandler.
    /// </summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }

        public PermissionRequirement(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                throw new ArgumentException("Permission cannot be empty.", nameof(permission));
            Permission = permission;
        }
    }
}
