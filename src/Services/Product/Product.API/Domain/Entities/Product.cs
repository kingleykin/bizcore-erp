using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;

namespace Product.API.Domain.Entities
{
    /// <summary>
    /// Sản phẩm/hàng hóa trong danh mục bán hàng. Mã sản phẩm do hệ thống tự sinh.
    /// </summary>
    public class Product : BaseEntity
    {
        // internal set: cho phép DbSeeder gán mã cố định cho dữ liệu demo; API luôn tạo mã qua Create().
        public string  Code        { get; internal set; } = null!;
        public string  Name        { get; private set; } = null!;
        public string  Unit        { get; private set; } = null!;
        public decimal Price       { get; private set; }
        public string? Description { get; private set; }
        public bool    IsActive    { get; private set; }

        private Product() { }

        public static Product Create(
            string name,
            string unit,
            decimal price,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên sản phẩm không được để trống.", new { field = nameof(Name) });
            if (string.IsNullOrWhiteSpace(unit))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Đơn vị tính không được để trống.", new { field = nameof(Unit) });
            if (price < 0)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Giá bán không được âm.", new { field = nameof(Price) });

            var product = new Product
            {
                Name        = name.Trim(),
                Unit        = unit.Trim(),
                Price       = price,
                Description = description?.Trim(),
                IsActive    = true
            };
            product.Code = $"SP{DateTime.UtcNow:yyMMdd}{product.Id.ToString("N")[..6].ToUpperInvariant()}";

            return product;
        }

        public void Update(string name, string unit, decimal price, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Tên sản phẩm không được để trống.", new { field = nameof(Name) });
            if (string.IsNullOrWhiteSpace(unit))
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Đơn vị tính không được để trống.", new { field = nameof(Unit) });
            if (price < 0)
                throw new DomainException(ErrorCodes.Common.InvalidRequest, "Giá bán không được âm.", new { field = nameof(Price) });

            Name        = name.Trim();
            Unit        = unit.Trim();
            Price       = price;
            Description = description?.Trim();
            UpdateState();
        }

        public void Activate()   { IsActive = true;  UpdateState(); }
        public void Deactivate() { IsActive = false; UpdateState(); }
    }
}
