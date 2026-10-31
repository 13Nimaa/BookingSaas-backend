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
    }
}
