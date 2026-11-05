using Application.Dtos.Business;
using FluentValidation;

namespace Application.Validators.Business;

public class BusinessListQueryValidator : AbstractValidator<BusinessListQuery>
{
    private static readonly HashSet<string> AllowedSortBy =
        new(StringComparer.OrdinalIgnoreCase) { "name", "city", "id" };

    private static readonly HashSet<string> AllowedSortOrder =
        new(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };

    public BusinessListQueryValidator()
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
            .WithMessage("مرتب‌سازی فقط بر اساس name، city یا id مجاز است.");

        RuleFor(x => x.SortOrder)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedSortOrder.Contains(s.Trim()))
            .WithMessage("ترتیب مرتب‌سازی باید asc یا desc باشد.");
    }
}
