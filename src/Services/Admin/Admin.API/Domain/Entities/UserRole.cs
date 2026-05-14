using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities
{
    /// <summary>
    /// Bảng trung gian User ↔ Role (many-to-many).
    /// </summary>
    public class UserRole : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;


        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}
