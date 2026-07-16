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
using Rent.Web.Infrastructure.Localization;
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
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IValidator<ListingFormInput> validator,
        IImageStorage storage,
        ILogger<EditModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
        _storage = storage;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public ListingFormInput Input { get; set; } = new();

    public bool ListingMissing { get; private set; }
    public IReadOnlyList<PropertyImage> ExistingImages { get; private set; } = [];
    public IReadOnlyList<Amenity> Amenities { get; private set; } = [];
    public IReadOnlyList<City> Cities { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var property = await _db.Properties
            .Include(p => p.Units)
            .Include(p => p.Images)
            .Include(p => p.Amenities)
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);

        if (property is null)
        {
            ListingMissing = true;
            return Page();
        }

        await LoadLookupsAsync(ct);

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
            Status = property.Status,
            LeaseTerm = property.LeaseTerm,
            ParkingType = property.ParkingType,
            YearBuilt = property.YearBuilt,
            TotalFloors = property.TotalFloors,
            AmenityIds = property.Amenities.Select(a => a.Id).ToList(),
            // TODAS las unidades: antes solo se exponia la mas barata y las demas eran
            // invisibles e ineditables para su dueño, aunque los inquilinos si las veian.
            Units = property.Units
                .OrderBy(u => u.Price)
                .Select(u => new UnitInput
                {
                    Id = u.Id,
                    Bedrooms = u.Bedrooms,
                    Bathrooms = u.Bathrooms,
                    SqFt = u.SqFt,
                    Price = u.Price,
                    AvailableDate = u.AvailableDate,
                    AvailableUnits = u.AvailableUnits
                })
                .ToList()
        };

        if (Input.Units.Count == 0) Input.Units.Add(new UnitInput());

        ExistingImages = property.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        [FromForm] Guid? deleteImageId,
        [FromForm] Guid? coverImageId,
        CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        // Acciones de foto: salen del mismo form (formnovalidate) y no guardan el resto.
        if (deleteImageId is not null || coverImageId is not null)
            return await HandlePhotoActionAsync(userId, deleteImageId, coverImageId, ct);

        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName}", err.ErrorMessage);
            await LoadExistingAsync(ct);
            await LoadLookupsAsync(ct);
            return Page();
        }

        var property = await _db.Properties
            .Include(p => p.Units)
            .Include(p => p.Images)
            .Include(p => p.Amenities)
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
        property.Status = Input.Status;
        property.LeaseTerm = Input.LeaseTerm;
        property.ParkingType = string.IsNullOrWhiteSpace(Input.ParkingType) ? null : Input.ParkingType.Trim();
        property.YearBuilt = Input.YearBuilt;
        property.TotalFloors = Input.TotalFloors;
        property.UpdatedAt = DateTimeOffset.UtcNow;

        var chosen = Input.AmenityIds.Count == 0
            ? []
            : await _db.Amenities.Where(a => Input.AmenityIds.Contains(a.Id)).ToListAsync(ct);
        property.Amenities.Clear();
        foreach (var a in chosen) property.Amenities.Add(a);

        // El Slug NO se regenera: se fija al crear. Regenerarlo cambiaba la URL publica al
        // editar el titulo y dejaba en 404 los bookmarks y los links ya enviados por email.

        // Merge por Id: NO borrar-y-recrear. Recrear romperia las FK y perderia CreatedAt.
        var keptIds = Input.Units.Where(u => u.Id.HasValue).Select(u => u.Id!.Value).ToHashSet();
        foreach (var removed in property.Units.Where(u => !keptIds.Contains(u.Id)).ToList())
        {
            // Solo la coleccion: Unit.PropertyId es requerido, asi que EF borra el huerfano en
            // cascada. Anadir tambien _db.Units.Remove emite un segundo DELETE de la misma fila
            // y el segundo afecta 0 filas -> DbUpdateConcurrencyException.
            property.Units.Remove(removed);
        }

        foreach (var input in Input.Units)
        {
            var unit = input.Id.HasValue ? property.Units.FirstOrDefault(u => u.Id == input.Id.Value) : null;
            if (unit is null)
            {
                // Add explicito en el DbSet: Unit.Id se inicializa con Guid.NewGuid(), asi que al
                // añadirla solo por la navegacion EF ve la clave puesta, la cree existente y emite
                // UPDATE en vez de INSERT (0 filas -> DbUpdateConcurrencyException).
                unit = new Unit { Id = Guid.NewGuid(), PropertyId = property.Id };
                _db.Units.Add(unit);
                property.Units.Add(unit);
            }
            unit.Bedrooms = input.Bedrooms;
            unit.Bathrooms = input.Bathrooms;
            unit.SqFt = input.SqFt;
            unit.Price = input.Price;
            unit.AvailableDate = input.AvailableDate;
            unit.AvailableUnits = input.AvailableUnits;
            unit.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (Input.NewImages is { Count: > 0 })
        {
            var nextOrder = property.Images.Count == 0 ? 0 : property.Images.Max(i => i.DisplayOrder) + 1;
            var hasPrimary = property.Images.Any(i => i.IsPrimary);
            var room = Math.Max(0, 10 - property.Images.Count);
            if (Input.NewImages.Count > room) TempData["ListingWarning"] = "landlord.imageTooMany";

            var rejected = false;
            foreach (var file in Input.NewImages.Take(room))
            {
                if (file.Length == 0) continue;
                try
                {
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
                catch (Exception ex)
                {
                    // Un archivo rechazado no debe tumbar el guardado entero.
                    _logger.LogWarning(ex, "Rejected image {FileName} for property {PropertyId}", file.FileName, property.Id);
                    rejected = true;
                }
            }
            if (rejected) TempData["ListingWarning"] = "landlord.imageRejected";
        }

        await _db.SaveChangesAsync(ct);

        TempData["ListingSuccess"] = "landlord.listingUpdated";
        return Redirect(this.Localized("/landlord/listings"));
    }

    private async Task<IActionResult> HandlePhotoActionAsync(Guid userId, Guid? deleteImageId, Guid? coverImageId, CancellationToken ct)
    {
        // El filtro de propiedad va DENTRO de la consulta: el rol Landlord por si solo no basta.
        var property = await _db.Properties
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);
        if (property is null) { ListingMissing = true; return Page(); }

        if (deleteImageId is not null)
        {
            var img = property.Images.FirstOrDefault(i => i.Id == deleteImageId);
            if (img is not null)
            {
                try { await _storage.DeleteAsync(img.Url, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for image {ImageId}", img.Id); }

                var wasPrimary = img.IsPrimary;
                property.Images.Remove(img);
                _db.PropertyImages.Remove(img);

                // Si se borro la portada, promover la siguiente para no dejar la ficha sin cover.
                if (wasPrimary)
                {
                    var next = property.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault();
                    if (next is not null) next.IsPrimary = true;
                }
                await _db.SaveChangesAsync(ct);
                TempData["ListingSuccess"] = "landlord.photoDeleted";
            }
        }
        else if (coverImageId is not null && property.Images.Any(i => i.Id == coverImageId))
        {
            foreach (var i in property.Images) i.IsPrimary = i.Id == coverImageId;
            await _db.SaveChangesAsync(ct);
            TempData["ListingSuccess"] = "landlord.listingUpdated";
        }

        return Redirect(this.Localized($"/landlord/listings/edit/{Id}"));
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

    private async Task LoadLookupsAsync(CancellationToken ct)
    {
        Amenities = await _db.Amenities.AsNoTracking().OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync(ct);
        Cities = await _db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
    }
}
