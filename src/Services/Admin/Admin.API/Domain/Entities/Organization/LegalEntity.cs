using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities.Organization
{
    /// <summary>
    /// Pháp nhân độc lập — có mã số thuế riêng, xuất báo cáo tài chính riêng.
    /// Đây là root aggregate của cây tổ chức doanh nghiệp.
    /// </summary>
    public class LegalEntity : BaseEntity
    {

        public string Code               { get; private set; } = null!;  // VD: 'BIZCORE-VN'
        public string Name               { get; private set; } = null!;
        public string? TaxCode           { get; private set; }
        public string? RegistrationNumber { get; private set; }
        public string? Address           { get; private set; }
        public string? BaseCurrencyCode  { get; private set; }           // VD: 'VND'
        public int    Status             { get; private set; }           // 1=Active, 0=Inactive



        // Navigation
        public ICollection<Branch>     Branches    { get; private set; } = new List<Branch>();
        public ICollection<CostCenter> CostCenters { get; private set; } = new List<CostCenter>();

        private LegalEntity() { }

        public static LegalEntity Create(
            string  code,
            string  name,
            string? taxCode            = null,
            string? registrationNumber = null,
            string? address            = null,
            string? baseCurrencyCode   = "VND")
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return new LegalEntity
            {
                Code               = code.Trim().ToUpperInvariant(),
                Name               = name.Trim(),
                TaxCode            = taxCode?.Trim(),
                RegistrationNumber = registrationNumber?.Trim(),
                Address            = address?.Trim(),
                BaseCurrencyCode   = baseCurrencyCode?.Trim().ToUpperInvariant() ?? "VND",
                Status             = 1
            };

        }

        public void Update(
            string  name,
            string? taxCode            = null,
            string? registrationNumber = null,
            string? address            = null,
            string? baseCurrencyCode   = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            Name               = name.Trim();
            TaxCode            = taxCode?.Trim();
            RegistrationNumber = registrationNumber?.Trim();
            Address            = address?.Trim();
            if (!string.IsNullOrWhiteSpace(baseCurrencyCode))
                BaseCurrencyCode = baseCurrencyCode.Trim().ToUpperInvariant();
            UpdateTimestamp();

        }

        public void Deactivate() { Status = 0; UpdateTimestamp(); }
        public void Activate()   { Status = 1; UpdateTimestamp(); }
    }
}
