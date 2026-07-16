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

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone number is too long.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
