using FluentValidation;
using Rent.Web.Features.LandlordManage.Pages.Listings;

namespace Rent.Web.Features.LandlordManage.Validators;

public class ListingFormValidator : AbstractValidator<ListingFormInput>
{
    public ListingFormValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.StreetAddress).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CityName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Province).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(10);

        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.Bathrooms).InclusiveBetween(0.5m, 20m);
        RuleFor(x => x.Price).GreaterThan(0).LessThan(100000);
        RuleFor(x => x.SqFt).InclusiveBetween(1, 20000).When(x => x.SqFt.HasValue);
    }
}
