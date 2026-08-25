using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Customer.API.Domain.Entities
{
    /// <summary>
    /// Nhóm khách hàng dùng để phân loại Customer (VD: VIP, Bán lẻ, Đại lý).
    /// </summary>
    public class CustomerGroup : BaseEntity
    {
        public string  Code        { get; private set; } = null!;
        public string  Name        { get; private set; } = null!;
        public string? Description { get; private set; }
        public bool    IsActive    { get; private set; }

        // Navigation
        public ICollection<Customer> Customers { get; private set; } = new List<Customer>();

        private CustomerGroup() { }

        public static CustomerGroup Create(string code, string name, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Mã nhóm khách hàng không được để trống.", new { field = nameof(Code) });
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên nhóm khách hàng không được để trống.", new { field = nameof(Name) });

            return new CustomerGroup
            {
                Code        = code.Trim().ToUpperInvariant(),
                Name        = name.Trim(),
                Description = description?.Trim(),
                IsActive    = true
            };
        }

        public void Update(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên nhóm khách hàng không được để trống.", new { field = nameof(Name) });

            Name        = name.Trim();
            Description = description?.Trim();
            UpdateState();
        }

        public void Activate()   { IsActive = true;  UpdateState(); }
        public void Deactivate() { IsActive = false; UpdateState(); }
    }
}
