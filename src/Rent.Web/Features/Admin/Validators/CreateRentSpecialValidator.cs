using FluentValidation;

namespace Rent.Web.Features.Admin.Validators;

public class CreateRentSpecialValidator : AbstractValidator<CreateRentSpecialRequest>
{
    public CreateRentSpecialValidator()
    {
        RuleFor(x => x.PropertyId).NotEqual(Guid.Empty).WithMessage("Pick a property.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.StartDate <= x.EndDate)
            .WithMessage("Start date must be on or before end date.")
            .OverridePropertyName(nameof(CreateRentSpecialRequest.EndDate));
    }
}

public class UpdateRentSpecialValidator : AbstractValidator<UpdateRentSpecialRequest>
{
    public UpdateRentSpecialValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.StartDate <= x.EndDate)
            .WithMessage("Start date must be on or before end date.")
            .OverridePropertyName(nameof(UpdateRentSpecialRequest.EndDate));
    }
}
