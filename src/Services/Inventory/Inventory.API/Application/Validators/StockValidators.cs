using FluentValidation;
using Inventory.API.Application.DTOs;

namespace Inventory.API.Application.Validators;

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
            .MaximumLength(255).WithMessage("Tên sản phẩm không quá 255 ký tự");

        RuleFor(x => x.QuantityOnHand)
            .GreaterThanOrEqualTo(0).WithMessage("Tồn kho không được âm");
    }
}
