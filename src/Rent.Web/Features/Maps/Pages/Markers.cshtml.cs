using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Features.Search;

namespace Rent.Web.Features.Maps.Pages;

[AllowAnonymous]
public class MarkersModel : PageModel
{
    private readonly MapMarkersHandler _handler;

    public MarkersModel(MapMarkersHandler handler)
    {
        _handler = handler;
    }

    [BindProperty(SupportsGet = true)]
    public SearchQuery Query { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string citySlug, CancellationToken ct)
    {
        Query.CitySlug = citySlug;

        var result = await _handler.ExecuteAsync(Query, ct);
        if (!result.CityFound)
        {
            return new JsonResult(new { error = "city_not_found" }) { StatusCode = StatusCodes.Status404NotFound };
        }

        return new JsonResult(new
        {
            city = new { lat = result.CityLat, lng = result.CityLng },
            markers = result.Markers.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                slug = m.Slug,
                citySlug = m.CitySlug,
                lat = m.Lat,
                lng = m.Lng,
                fromPrice = m.FromPrice,
                minBedrooms = m.MinBedrooms,
                propertyType = m.PropertyType.ToString(),
                tier = m.Tier.ToString(),
                primaryImageUrl = m.PrimaryImageUrl
            })
        });
    }
}
