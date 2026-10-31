using Application.Dtos.Customer;
using FluentValidation;

namespace Application.Validators.Customer;

public class CustomerListQueryValidator : AbstractValidator<CustomerListQuery>
{
    private static readonly HashSet<string> AllowedSortBy =
        new(StringComparer.OrdinalIgnoreCase) { "createdAt", "name", "updatedAt" };

    private static readonly HashSet<string> AllowedSortOrder =
        new(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };

    public CustomerListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("شماره صفحه باید حداقل ۱ باشد.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("اندازه صفحه باید بیشتر از ۰ باشد.")
            .LessThanOrEqualTo(100)
            .WithMessage("اندازه صفحه نمی‌تواند بیشتر از ۱۰۰ باشد.");

        RuleFor(x => x.SortBy)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedSortBy.Contains(s.Trim()))
            .WithMessage("مرتب‌سازی فقط بر اساس createdAt، name یا updatedAt مجاز است.");

        RuleFor(x => x.SortOrder)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedSortOrder.Contains(s.Trim()))
            .WithMessage("ترتیب مرتب‌سازی باید asc یا desc باشد.");

        RuleFor(x => x)
            .Must(x => x.CreatedFrom is null || x.CreatedTo is null || x.CreatedFrom <= x.CreatedTo)
            .WithMessage("تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.");
    }
}
