using Application.Dtos.Booking;
using FluentValidation;

namespace Application.Validators.Booking;

public class BookingListQueryValidator : AbstractValidator<BookingListQuery>
{
    public BookingListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("شماره صفحه باید حداقل ۱ باشد.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("اندازه صفحه باید بیشتر از ۰ باشد.")
            .LessThanOrEqualTo(100)
            .WithMessage("اندازه صفحه نمی‌تواند بیشتر از ۱۰۰ باشد.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status is not null)
            .WithMessage("وضعیت درخواست‌شده معتبر نیست.");

        RuleFor(x => x)
            .Must(x => x.FromDate is null || x.ToDate is null || x.FromDate <= x.ToDate)
            .WithMessage("تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.");
    }
}
