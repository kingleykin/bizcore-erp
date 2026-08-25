using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Customer.API.Domain.Entities
{
    /// <summary>
    /// Khách hàng của tổ chức. Mỗi Customer thuộc về một CustomerGroup.
    /// </summary>
    public class Customer : BaseEntity
    {
        public string  Code            { get; private set; } = null!;
        public string  Name            { get; private set; } = null!;
        public string? TaxCode         { get; private set; }
        public string? Email           { get; private set; }
        public string? Phone           { get; private set; }
        public string? Address         { get; private set; }
        public Guid    CustomerGroupId { get; private set; }
        public bool    IsActive        { get; private set; }

        // Navigation
        public CustomerGroup CustomerGroup { get; private set; } = null!;

        private Customer() { }

        public static Customer Create(
            string code,
            string name,
            Guid customerGroupId,
            string? taxCode = null,
            string? email = null,
            string? phone = null,
            string? address = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Mã khách hàng không được để trống.", new { field = nameof(Code) });
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên khách hàng không được để trống.", new { field = nameof(Name) });
            if (customerGroupId == Guid.Empty)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Nhóm khách hàng không được để trống.", new { field = nameof(CustomerGroupId) });

            return new Customer
            {
                Code            = code.Trim().ToUpperInvariant(),
                Name            = name.Trim(),
                CustomerGroupId = customerGroupId,
                TaxCode         = taxCode?.Trim(),
                Email           = email?.Trim(),
                Phone           = phone?.Trim(),
                Address         = address?.Trim(),
                IsActive        = true
            };
        }

        public void Update(string name, string? taxCode, string? email, string? phone, string? address)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên khách hàng không được để trống.", new { field = nameof(Name) });

            Name    = name.Trim();
            TaxCode = taxCode?.Trim();
            Email   = email?.Trim();
            Phone   = phone?.Trim();
            Address = address?.Trim();
            UpdateState();
        }

        public void ChangeGroup(Guid customerGroupId)
        {
            if (customerGroupId == Guid.Empty)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Nhóm khách hàng không được để trống.", new { field = nameof(CustomerGroupId) });

            CustomerGroupId = customerGroupId;
            UpdateState();
        }

        public void Activate()   { IsActive = true;  UpdateState(); }
        public void Deactivate() { IsActive = false; UpdateState(); }
    }
}
