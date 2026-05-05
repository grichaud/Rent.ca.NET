using FluentValidation;

namespace Rent.Web.Features.Admin.Validators;

public class SetTierValidator : AbstractValidator<SetTierRequest>
{
    public SetTierValidator()
    {
        RuleFor(x => x.TargetId)
            .NotEqual(Guid.Empty).WithMessage("Target id is required.");

        RuleFor(x => x.Tier)
            .IsInEnum().WithMessage("Pick a valid tier.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTimeOffset.UtcNow).WithMessage("Expiration must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}
