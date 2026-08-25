using FluentValidation;
using Customer.API.Application.DTOs;

namespace Customer.API.Application.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã khách hàng không được để trống")
            .MaximumLength(50).WithMessage("Mã khách hàng không quá 50 ký tự");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên khách hàng không được để trống")
            .MaximumLength(255).WithMessage("Tên khách hàng không quá 255 ký tự");

        RuleFor(x => x.CustomerGroupId)
            .NotEmpty().WithMessage("Nhóm khách hàng không được để trống");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ");
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên khách hàng không được để trống")
            .MaximumLength(255).WithMessage("Tên khách hàng không quá 255 ký tự");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ");
    }
}
