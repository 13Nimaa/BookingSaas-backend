using Application.Dtos.Booking;
using FluentValidation;

namespace Application.Validators.Booking;

public class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator()
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
    }
}
