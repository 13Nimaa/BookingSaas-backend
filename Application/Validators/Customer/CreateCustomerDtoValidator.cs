using Application.Dtos.Customer;
using FluentValidation;

namespace Application.Validators.Customer;

public class CreateCustomerDtoValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(150).WithMessage("نام نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^(\+98|0)?9\d{9}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("شماره موبایل معتبر نیست.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("ایمیل معتبر نیست.")
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("ایمیل نمی‌تواند بیشتر از ۲۵۶ کاراکتر باشد.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes is not null)
            .WithMessage("یادداشت نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد.");
    }
}
