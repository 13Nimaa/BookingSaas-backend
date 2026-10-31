using Application.Dtos.Booking;
using FluentValidation;

namespace Application.Validators.Booking;

public class CreateGuestBookingDtoValidator : AbstractValidator<CreateGuestBookingDto>
{
    public CreateGuestBookingDtoValidator()
    {
        RuleFor(x => x.BusinessId)
            .GreaterThan(0)
            .WithMessage("شناسه کسب‌وکار معتبر نیست.");

        RuleFor(x => x.ServiceId)
            .GreaterThan(0)
            .WithMessage("شناسه خدمت معتبر نیست.");

        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("شناسه کارمند معتبر نیست.");

        RuleFor(x => x.StartTime)
            .NotEqual(default(DateTime))
            .WithMessage("زمان شروع معتبر نیست.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(150).WithMessage("نام نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("شماره موبایل الزامی است.")
            .Matches(@"^(\+98|0)?9\d{9}$")
            .WithMessage("شماره موبایل معتبر نیست.");
    }
}
