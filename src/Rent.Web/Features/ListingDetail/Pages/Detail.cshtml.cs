using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.ListingDetail.Pages;

[AllowAnonymous]
public class DetailModel : PageModel
{
    private readonly AppDbContext _db;

    public DetailModel(AppDbContext db)
    {
        _db = db;
    }

    public Property? Property { get; private set; }
    public string CitySlug { get; private set; } = string.Empty;

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

        return Page();
    }
}
