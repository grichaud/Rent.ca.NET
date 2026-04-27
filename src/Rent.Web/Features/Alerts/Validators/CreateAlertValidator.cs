using FluentValidation;

namespace Rent.Web.Features.Alerts.Validators;

public class CreateAlertValidator : AbstractValidator<CreateAlertRequest>
{
    public CreateAlertValidator()
    {
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100);

        RuleFor(x => x.PriceMin)
            .GreaterThanOrEqualTo(0).WithMessage("Min price must be 0 or greater.")
            .When(x => x.PriceMin.HasValue);

        RuleFor(x => x.PriceMax)
            .GreaterThan(0).WithMessage("Max price must be greater than 0.")
            .When(x => x.PriceMax.HasValue);

        RuleFor(x => x)
            .Must(x => !x.PriceMin.HasValue || !x.PriceMax.HasValue || x.PriceMin.Value <= x.PriceMax.Value)
            .WithMessage("Min price cannot be greater than max price.")
            .OverridePropertyName(nameof(CreateAlertRequest.PriceMax));

        RuleFor(x => x.BedroomsMin)
            .InclusiveBetween(0, 10).WithMessage("Bedrooms must be between 0 and 10.")
            .When(x => x.BedroomsMin.HasValue);

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Pick a valid frequency.");

        RuleFor(x => x.PetsAllowed)
            .Must(v => string.IsNullOrEmpty(v) || v == "true" || v == "false")
            .WithMessage("Pets value is invalid.");
    }
}
