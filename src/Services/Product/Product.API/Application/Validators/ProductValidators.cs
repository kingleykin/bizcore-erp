using FluentValidation;
using Product.API.Application.DTOs;

namespace Product.API.Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
            .MaximumLength(255).WithMessage("Tên sản phẩm không quá 255 ký tự");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Đơn vị tính không được để trống")
            .MaximumLength(30).WithMessage("Đơn vị tính không quá 30 ký tự");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Giá bán không được âm");
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
            .MaximumLength(255).WithMessage("Tên sản phẩm không quá 255 ký tự");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Đơn vị tính không được để trống")
            .MaximumLength(30).WithMessage("Đơn vị tính không quá 30 ký tự");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Giá bán không được âm");
    }
}
