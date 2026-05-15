using FluentValidation;
using Invoice.API.Application.DTOs;

namespace Invoice.API.Application.Validators;

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Tên khách hàng không được để trống")
            .MaximumLength(100).WithMessage("Tên khách hàng không quá 100 ký tự");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0");
    }
}
