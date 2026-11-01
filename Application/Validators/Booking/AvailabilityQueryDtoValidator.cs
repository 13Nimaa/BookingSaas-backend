using Application.Dtos.Booking;
using FluentValidation;

namespace Application.Validators.Booking;

public class AvailabilityQueryDtoValidator : AbstractValidator<AvailabilityQueryDto>
{
    public AvailabilityQueryDtoValidator()
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

        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("تاریخ معتبر نیست.");
    }
}
