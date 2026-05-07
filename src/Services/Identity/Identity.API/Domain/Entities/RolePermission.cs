namespace Identity.API.Domain.Entities
{
    /// <summary>
    /// Bảng trung gian Role ↔ Permission (many-to-many).
    /// </summary>
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Guid PermissionId { get; set; }

        public Role Role { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
}
