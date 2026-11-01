using Application.Dtos.Booking;
using FluentValidation;

namespace Application.Validators.Booking;

public class UpdateBookingStatusDtoValidator : AbstractValidator<UpdateBookingStatusDto>
{
    public UpdateBookingStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("وضعیت درخواست‌شده معتبر نیست.");
    }
}
