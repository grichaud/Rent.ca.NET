using FluentValidation;
using Rent.Web.Features.LandlordManage.Pages;

namespace Rent.Web.Features.LandlordManage.Validators;

public class LandlordProfileValidator : AbstractValidator<AccountModel.LandlordProfileInput>
{
    public LandlordProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone number is too long.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.CompanyName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CompanyName));

        RuleFor(x => x.Website)
            .MaximumLength(300)
            .Must(w => Uri.TryCreate(w, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Website must be a valid URL starting with http:// or https://")
            .When(x => !string.IsNullOrWhiteSpace(x.Website));

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
