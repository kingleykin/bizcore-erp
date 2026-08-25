using FluentValidation;
using Customer.API.Application.DTOs;

namespace Customer.API.Application.Validators;

public class CreateCustomerGroupRequestValidator : AbstractValidator<CreateCustomerGroupRequest>
{
    public CreateCustomerGroupRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã nhóm khách hàng không được để trống")
            .MaximumLength(50).WithMessage("Mã nhóm khách hàng không quá 50 ký tự");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nhóm khách hàng không được để trống")
            .MaximumLength(255).WithMessage("Tên nhóm khách hàng không quá 255 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không quá 500 ký tự");
    }
}

public class UpdateCustomerGroupRequestValidator : AbstractValidator<UpdateCustomerGroupRequest>
{
    public UpdateCustomerGroupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nhóm khách hàng không được để trống")
            .MaximumLength(255).WithMessage("Tên nhóm khách hàng không quá 255 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không quá 500 ký tự");
    }
}
