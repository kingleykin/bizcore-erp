namespace Identity.API.Domain.Entities
{
    /// <summary>
    /// Audit Log — ghi nhận mọi hành động quan trọng (tạo/sửa/xóa user, đổi quyền).
    /// Production-ready: không xóa được, chỉ ghi thêm.
    /// </summary>
    public class AuditLog
    {
        public Guid Id { get; private set; }

        /// <summary>User nào thực hiện hành động (null = system/seed).</summary>
        public Guid? ActorUserId { get; private set; }
        public string? ActorUsername { get; private set; }

        /// <summary>Ví dụ: "User.Create", "Role.AssignPermission", "Auth.Login.Failed"</summary>
        public string Action { get; private set; } = null!;

        /// <summary>Entity bị tác động: "User", "Role", v.v.</summary>
        public string? TargetEntityType { get; private set; }
        public string? TargetEntityId { get; private set; }

        /// <summary>JSON snapshot của thay đổi (trước/sau).</summary>
        public string? Metadata { get; private set; }

        public string? IpAddress { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private AuditLog() { }

        public static AuditLog Create(
            string action,
            Guid? actorUserId = null,
            string? actorUsername = null,
            string? targetEntityType = null,
            string? targetEntityId = null,
            string? metadata = null,
            string? ipAddress = null)
        {
            return new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = action,
                ActorUserId = actorUserId,
                ActorUsername = actorUsername,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                Metadata = metadata,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
