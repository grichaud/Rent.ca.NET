using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Shared.Services;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Storage;

namespace Rent.Web.Features.LandlordManage.Pages.Listings;

[Authorize(Roles = Roles.Landlord)]
[RequestSizeLimit(40 * 1024 * 1024)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<ListingFormInput> _validator;
    private readonly IImageStorage _storage;

    public EditModel(AppDbContext db, UserManager<ApplicationUser> userManager, IValidator<ListingFormInput> validator, IImageStorage storage)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public ListingFormInput Input { get; set; } = new();

    public bool ListingMissing { get; private set; }
    public IReadOnlyList<PropertyImage> ExistingImages { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var property = await _db.Properties
            .Include(p => p.Units)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);

        if (property is null)
        {
            ListingMissing = true;
            return Page();
        }

        var unit = property.Units.OrderBy(u => u.Price).First();
        Input = new ListingFormInput
        {
            Title = property.Title,
            Description = property.Description,
            PropertyType = property.PropertyType,
            StreetAddress = property.StreetAddress,
            CityName = property.City,
            Province = property.Province,
            PostalCode = property.PostalCode,
            Neighbourhood = property.Neighbourhood,
            PetsAllowed = property.PetsAllowed,
            Furnished = property.Furnished,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            SqFt = unit.SqFt,
            Price = unit.Price,
            AvailableDate = unit.AvailableDate
        };

        ExistingImages = property.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName}", err.ErrorMessage);
            await LoadExistingAsync(ct);
            return Page();
        }

        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var property = await _db.Properties
            .Include(p => p.Units)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);

        if (property is null)
        {
            ListingMissing = true;
            return Page();
        }

        property.Title = Input.Title.Trim();
        property.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        property.PropertyType = Input.PropertyType;
        property.StreetAddress = Input.StreetAddress.Trim();
        property.City = Input.CityName.Trim();
        property.Province = Input.Province.Trim();
        property.PostalCode = Input.PostalCode.Trim();
        property.Neighbourhood = string.IsNullOrWhiteSpace(Input.Neighbourhood) ? null : Input.Neighbourhood.Trim();
        property.PetsAllowed = Input.PetsAllowed;
        property.Furnished = Input.Furnished;
        property.UpdatedAt = DateTimeOffset.UtcNow;

        var baseSlug = SlugGenerator.From(property.Title);
        property.Slug = await SlugGenerator.UniqueAsync(_db, baseSlug, property.Id, ct);

        var unit = property.Units.OrderBy(u => u.Price).First();
        unit.Bedrooms = Input.Bedrooms;
        unit.Bathrooms = Input.Bathrooms;
        unit.SqFt = Input.SqFt;
        unit.Price = Input.Price;
        unit.AvailableDate = Input.AvailableDate;
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        if (Input.NewImages is { Count: > 0 })
        {
            var nextOrder = property.Images.Count == 0 ? 0 : property.Images.Max(i => i.DisplayOrder) + 1;
            var hasPrimary = property.Images.Any(i => i.IsPrimary);
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
                    IsPrimary = !hasPrimary,
                    DisplayOrder = nextOrder++
                });
                hasPrimary = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        TempData["ListingSuccess"] = $"Changes to \"{property.Title}\" saved.";
        return Redirect("/landlord/listings");
    }

    private async Task LoadExistingAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        ExistingImages = await _db.PropertyImages
            .AsNoTracking()
            .Where(i => i.PropertyId == Id && i.Property.LandlordProfileId == userId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .ToListAsync(ct);
    }
}
