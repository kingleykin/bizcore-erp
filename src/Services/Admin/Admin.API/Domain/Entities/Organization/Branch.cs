using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities.Organization
{
    /// <summary>
    /// Chi nhánh phụ thuộc vào một LegalEntity.
    /// Mỗi chi nhánh có thể có nhiều phòng ban (Department).
    /// </summary>
    public class Branch : BaseEntity
    {
        public Guid   LegalEntityId { get; private set; }

        public string Code          { get; private set; } = null!;
        public string Name          { get; private set; } = null!;
        public string? Address      { get; private set; }
        public bool   IsActive      { get; private set; }



        // Navigation
        public LegalEntity              LegalEntity  { get; private set; } = null!;
        public ICollection<Department>  Departments  { get; private set; } = new List<Department>();

        private Branch() { }

        public static Branch Create(Guid legalEntityId, string code, string name, string? address = null)
        {
            if (legalEntityId == Guid.Empty)
                throw new ArgumentException("LegalEntityId is required.", nameof(legalEntityId));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return new Branch
            {
                LegalEntityId = legalEntityId,
                Code          = code.Trim().ToUpperInvariant(),
                Name          = name.Trim(),
                Address       = address?.Trim(),
                IsActive      = true
            };

        }

        public void Update(string name, string? address)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));
            Name      = name.Trim();
            Address   = address?.Trim();
            UpdateState();

        }

        public void Deactivate() { IsActive = false; UpdateState(); }
        public void Activate()   { IsActive = true;  UpdateState(); }
    }
}
