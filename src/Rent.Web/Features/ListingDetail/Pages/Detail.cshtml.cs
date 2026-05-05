using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Favorites;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.ListingDetail.Pages;

[AllowAnonymous]
public class DetailModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFavoriteService _favorites;

    public DetailModel(AppDbContext db, UserManager<ApplicationUser> userManager, IFavoriteService favorites)
    {
        _db = db;
        _userManager = userManager;
        _favorites = favorites;
    }

    public Property? Property { get; private set; }
    public string CitySlug { get; private set; } = string.Empty;
    public bool IsFavorited { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public async Task<IActionResult> OnGetAsync(string citySlug, string propertySlug, CancellationToken ct)
    {
        CitySlug = citySlug;

        var city = await _db.Cities.FirstOrDefaultAsync(c => c.Slug == citySlug, ct);
        if (city is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        Property = await _db.Properties
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Units)
            .Include(p => p.Amenities)
            .Include(p => p.RentSpecials)
            .FirstOrDefaultAsync(p =>
                p.Slug == propertySlug &&
                p.City == city.Name &&
                p.Status == ListingStatus.Active, ct);

        if (Property is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        await _db.Properties
            .Where(p => p.Id == Property.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(pp => pp.ViewCount, pp => pp.ViewCount + 1), ct);

        IsAuthenticated = User.Identity?.IsAuthenticated == true;
        if (IsAuthenticated && Guid.TryParse(_userManager.GetUserId(User), out var uid))
            IsFavorited = await _favorites.IsFavoritedAsync(uid, Property.Id, ct);

        return Page();
    }
}
