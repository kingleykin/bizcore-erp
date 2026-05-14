using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities.Organization
{
    /// <summary>
    /// Trung tâm chi phí (Cost Center) — thuộc một LegalEntity.
    /// Dùng trong phân hệ Kế toán để phân bổ chi phí.
    /// </summary>
    public class CostCenter : BaseEntity
    {
        public Guid   LegalEntityId { get; private set; }

        public string Code          { get; private set; } = null!;
        public string Name          { get; private set; } = null!;
        public bool   IsActive      { get; private set; }



        // Navigation
        public LegalEntity LegalEntity { get; private set; } = null!;

        private CostCenter() { }

        public static CostCenter Create(Guid legalEntityId, string code, string name)
        {
            if (legalEntityId == Guid.Empty)
                throw new ArgumentException("LegalEntityId is required.", nameof(legalEntityId));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return new CostCenter
            {
                LegalEntityId = legalEntityId,
                Code          = code.Trim().ToUpperInvariant(),
                Name          = name.Trim(),
                IsActive      = true
            };

        }

        public void Update(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));
            Name      = name.Trim();
            UpdateTimestamp();

        }

        public void Deactivate() { IsActive = false; UpdateTimestamp(); }
        public void Activate()   { IsActive = true;  UpdateTimestamp(); }
    }
}
