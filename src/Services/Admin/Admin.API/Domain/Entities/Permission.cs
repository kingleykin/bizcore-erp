using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities
{
    /// <summary>
    /// Đại diện cho một quyền cụ thể trong hệ thống.
    /// Convention: Code dạng "Invoice.View", "Menu.Invoice", "Invoice.Amount.Edit"
    /// </summary>
    public class Permission : BaseEntity
    {


        /// <summary>
        /// Mã định danh duy nhất, PascalCase dot-notation.
        /// Ví dụ: "Invoice.View", "Menu.Invoice", "Invoice.Amount.Edit"
        /// </summary>
        public string Code { get; private set; } = null!;

        /// <summary>
        /// Tên thân thiện hiển thị trong UI quản trị.
        /// </summary>
        public string Name { get; private set; } = null!;

        /// <summary>
        /// Resource bị tác động: "Invoice", "Payment", "Navigation.Invoice"
        /// </summary>
        public string Resource { get; private set; } = null!;

        /// <summary>
        /// Loại scope: Menu | Page | Action | Field | Data
        /// </summary>
        public string Scope { get; private set; } = null!;

        /// <summary>
        /// Mô tả chi tiết về permission.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Permission hệ thống — không được xóa.
        /// </summary>
        public bool IsSystem { get; private set; }

        // Navigation
        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

        private Permission() { }

        public static Permission Create(
            string code,
            string name,
            string resource,
            string scope,
            string? description = null,
            bool isSystem = true)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Permission code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(resource))
                throw new ArgumentException("Permission resource is required.", nameof(resource));

            return new Permission
            {
                Code        = code.Trim(),
                Name        = name.Trim(),
                Resource    = resource.Trim(),
                Scope       = scope.Trim(),
                Description = description,
                IsSystem    = isSystem
            };

        }

        public void UpdateDescription(string? description)
        {
            Description = description;
            UpdateState();
        }

    }
}
