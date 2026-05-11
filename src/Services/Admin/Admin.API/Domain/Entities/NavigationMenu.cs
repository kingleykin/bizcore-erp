namespace Admin.API.Domain.Entities
{
    /// <summary>
    /// Dynamic navigation menu item — được render động trên frontend
    /// dựa trên permissions của user hiện tại.
    /// </summary>
    public class NavigationMenu
    {
        public Guid Id { get; private set; }

        /// <summary>
        /// Parent menu item (null = root level).
        /// </summary>
        public Guid? ParentId { get; private set; }

        /// <summary>
        /// Tên hiển thị trên menu.
        /// </summary>
        public string Name { get; private set; } = null!;

        /// <summary>
        /// Route frontend: "/invoice", "/payment", v.v.
        /// </summary>
        public string Route { get; private set; } = null!;

        /// <summary>
        /// Icon identifier (tên icon trong thư viện frontend).
        /// </summary>
        public string? Icon { get; private set; }

        /// <summary>
        /// Thứ tự hiển thị trong menu.
        /// </summary>
        public int SortOrder { get; private set; }

        /// <summary>
        /// Permission Code cần có để thấy menu item này.
        /// Ví dụ: "Menu.Invoice", "Menu.Payment"
        /// </summary>
        public string PermissionCode { get; private set; } = null!;

        /// <summary>
        /// Ẩn/hiện menu item (soft toggle cho admin, không xóa).
        /// </summary>
        public bool IsActive { get; private set; } = true;

        // Navigation
        public NavigationMenu? Parent { get; private set; }
        public ICollection<NavigationMenu> Children { get; private set; } = new List<NavigationMenu>();

        private NavigationMenu() { }

        public static NavigationMenu Create(
            string name,
            string route,
            string permissionCode,
            int sortOrder,
            string? icon = null,
            Guid? parentId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Menu name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Menu route is required.", nameof(route));
            if (string.IsNullOrWhiteSpace(permissionCode))
                throw new ArgumentException("Permission code is required.", nameof(permissionCode));

            return new NavigationMenu
            {
                Id             = Guid.NewGuid(),
                Name           = name.Trim(),
                Route          = route.Trim(),
                PermissionCode = permissionCode.Trim(),
                SortOrder      = sortOrder,
                Icon           = icon?.Trim(),
                ParentId       = parentId,
                IsActive       = true
            };
        }

        public void SetActive(bool isActive) => IsActive = isActive;
    }
}
