using FluentValidation;
using Rent.Web.Features.Auth.Pages;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Auth.Validators;

public class ExternalLoginConfirmValidator : AbstractValidator<ExternalLoginConfirmModel.InputModel>
{
    public ExternalLoginConfirmValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => r == Roles.Renter || r == Roles.Landlord)
            .WithMessage("Role must be Renter or Landlord.");
    }
}
