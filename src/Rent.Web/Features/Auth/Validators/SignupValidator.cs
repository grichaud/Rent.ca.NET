using FluentValidation;
using Rent.Web.Features.Auth.Pages;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Auth.Validators;

public class SignupValidator : AbstractValidator<SignupModel.InputModel>
{
    public SignupValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => r == Roles.Renter || r == Roles.Landlord)
            .WithMessage("Role must be Renter or Landlord.");
    }
}
