using Application.Dtos.Employee;
using FluentValidation;

namespace Application.Validators.Employee;

public class AssignServiceDtoValidator : AbstractValidator<AssignServiceDto>
{
    public AssignServiceDtoValidator()
    {
        RuleFor(x => x.ServiceId)
            .GreaterThan(0)
            .WithMessage("شناسه خدمت معتبر نیست.");

        When(x => x.PriceOverride.HasValue, () =>
        {
            RuleFor(x => x.PriceOverride!.Value)
                .GreaterThanOrEqualTo(0)
                .WithMessage("قیمت باید صفر یا بیشتر باشد.")
                .PrecisionScale(10, 2, ignoreTrailingZeros: false)
                .WithMessage("فرمت قیمت معتبر نیست.");
        });

        When(x => x.DurationMinutesOverride.HasValue, () =>
        {
            RuleFor(x => x.DurationMinutesOverride!.Value)
                .GreaterThan(0)
                .WithMessage("مدت زمان باید عدد مثبت باشد.");
        });
    }
}
