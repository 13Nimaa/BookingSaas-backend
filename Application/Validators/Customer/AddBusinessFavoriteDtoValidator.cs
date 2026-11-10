using Application.Dtos.Customer;
using FluentValidation;

namespace Application.Validators.Customer;

public sealed class AddBusinessFavoriteDtoValidator : AbstractValidator<AddBusinessFavoriteDto>
{
    public AddBusinessFavoriteDtoValidator()
    {
        RuleFor(x => x.BusinessId).GreaterThan(0);
    }
}
