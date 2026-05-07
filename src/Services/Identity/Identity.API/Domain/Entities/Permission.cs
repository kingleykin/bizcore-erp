namespace Identity.API.Domain.Entities
{
    /// <summary>
    /// Đại diện cho một quyền cụ thể trong hệ thống.
    /// Action map với các hằng số trong Bizcore.BuildingBlocks.Permissions.
    /// </summary>
    public class Permission
    {
        public Guid Id { get; private set; }

        /// <summary>
        /// Chuỗi định danh quyền, ví dụ: "invoice:create", "identity:users:view"
        /// </summary>
        public string Action { get; private set; } = null!;

        public string? Description { get; private set; }

        // Navigation
        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

        private Permission() { }

        public static Permission Create(string action, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Permission action is required.", nameof(action));

            return new Permission
            {
                Id = Guid.NewGuid(),
                Action = action.Trim().ToLowerInvariant(),
                Description = description
            };
        }
    }
}
