using FluentValidation;

namespace Rent.Web.Features.Admin.Validators;

public class UpdatePopularSearchRequest
{
    public Guid Id { get; set; }
    public string NormalizedQuery { get; set; } = string.Empty;
    public string? CitySlug { get; set; }
}

public class UpdatePopularSearchValidator : AbstractValidator<UpdatePopularSearchRequest>
{
    public UpdatePopularSearchValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.NormalizedQuery)
            .NotEmpty().WithMessage("Query cannot be empty.")
            .MaximumLength(200);
        RuleFor(x => x.CitySlug)
            .MaximumLength(100);
    }
}
