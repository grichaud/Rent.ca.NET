using FluentValidation;
using Rent.Web.Features.Auth.Pages;

namespace Rent.Web.Features.Auth.Validators;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordModel.InputModel>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress();

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset link is invalid or expired.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
