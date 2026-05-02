using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Features.Shared.Services;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;
using Rent.Web.Infrastructure.Storage;

namespace Rent.Web.Features.LandlordManage.Pages.Listings;

[Authorize(Roles = Roles.Landlord)]
[RequestSizeLimit(40 * 1024 * 1024)] // 40 MB total
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<ListingFormInput> _validator;
    private readonly IImageStorage _storage;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IValidator<ListingFormInput> validator,
        IImageStorage storage,
        ILogger<CreateModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
        _storage = storage;
        _logger = logger;
    }

    [BindProperty]
    public ListingFormInput Input { get; set; } = new();

    public void OnGet()
    {
        Input.AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName}", err.ErrorMessage);
            return Page();
        }

        var landlordId = Guid.Parse(_userManager.GetUserId(User)!);

        var baseSlug = SlugGenerator.From(Input.Title);
        var slug = await SlugGenerator.UniqueAsync(_db, baseSlug, null, ct);

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordProfileId = landlordId,
            Title = Input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            PropertyType = Input.PropertyType,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited,
            StreetAddress = Input.StreetAddress.Trim(),
            City = Input.CityName.Trim(),
            Province = Input.Province.Trim(),
            PostalCode = Input.PostalCode.Trim(),
            Neighbourhood = string.IsNullOrWhiteSpace(Input.Neighbourhood) ? null : Input.Neighbourhood.Trim(),
            Slug = slug,
            PetsAllowed = Input.PetsAllowed,
            Furnished = Input.Furnished,
            LeaseTerm = Domain.LeaseTerm.OneYear
        };

        property.Units.Add(new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Bedrooms = Input.Bedrooms,
            Bathrooms = Input.Bathrooms,
            SqFt = Input.SqFt,
            Price = Input.Price,
            AvailableDate = Input.AvailableDate,
            AvailableUnits = 1
        });

        if (Input.NewImages is { Count: > 0 })
        {
            var order = 0;
            foreach (var file in Input.NewImages.Take(10))
            {
                if (file.Length == 0) continue;
                var url = await _storage.SaveAsync(property.Id, file, ct);
                property.Images.Add(new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    Url = url,
                    AltText = property.Title,
                    IsPrimary = order == 0,
                    DisplayOrder = order++
                });
            }
        }

        _db.Properties.Add(property);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Landlord {LandlordId} created property {PropertyId} ({Slug})", landlordId, property.Id, property.Slug);

        TempData["ListingSuccess"] = $"&#10003; \"{property.Title}\" is live.";
        return Redirect(this.Localized("/landlord/listings"));
    }
}
