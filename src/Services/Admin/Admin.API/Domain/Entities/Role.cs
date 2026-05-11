namespace Admin.API.Domain.Entities
{
    public class Role
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public bool IsSystem { get; private set; } // System roles cannot be deleted (Admin, User)
        public DateTime CreatedAt { get; private set; }

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
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Description = description,
                IsSystem = isSystem,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Role name is required.", nameof(name));

            Name = name.Trim();
            Description = description;
        }
    }
}
