using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities
{
    public class Role : BaseEntity
    {

        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public bool IsSystem { get; private set; } // System roles cannot be deleted (Admin, User)


        // Navigation
        public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

        private Role() { }

        public static Role Create(string name, string? description = null, bool isSystem = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Role name is required.", nameof(name));

            return new Role
            {
                Name = name.Trim(),
                Description = description,
                IsSystem = isSystem
            };

        }

        public void Update(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Role name is required.", nameof(name));

            Name = name.Trim();
            Description = description;
            UpdateTimestamp();

        }
    }
}
