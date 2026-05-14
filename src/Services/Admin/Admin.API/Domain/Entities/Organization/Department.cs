using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities.Organization
{
    /// <summary>
    /// Phòng ban — hỗ trợ cây thư mục phân cấp (self-referencing hierarchy).
    /// Thuộc về một Branch. ParentId = null → phòng ban gốc.
    /// </summary>
    public class Department : BaseEntity
    {
        public Guid   BranchId { get; private set; }

        public Guid?  ParentId { get; private set; }   // null = root department
        public string Code     { get; private set; } = null!;
        public string Name     { get; private set; } = null!;



        // Navigation
        public Branch              Branch   { get; private set; } = null!;
        public Department?         Parent   { get; private set; }
        public ICollection<Department> Children { get; private set; } = new List<Department>();

        private Department() { }

        public static Department Create(Guid branchId, string code, string name, Guid? parentId = null)
        {
            if (branchId == Guid.Empty)
                throw new ArgumentException("BranchId is required.", nameof(branchId));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return new Department
            {
                BranchId  = branchId,
                ParentId  = parentId,
                Code      = code.Trim().ToUpperInvariant(),
                Name      = name.Trim()
            };

        }

        public void Update(string name, Guid? parentId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));
            Name      = name.Trim();
            ParentId  = parentId;
            UpdateTimestamp();

        }
    }
}
