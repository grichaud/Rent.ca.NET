using FluentValidation;
using Rent.Web.Features.RenterPortal.Pages;

namespace Rent.Web.Features.RenterPortal.Validators;

public class AccountProfileValidator : AbstractValidator<AccountModel.ProfileInput>
{
    public AccountProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);
    }
}
