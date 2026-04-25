using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Search;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Home;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<City> FeaturedCities { get; private set; } = [];
    public IReadOnlyList<City> AllCities { get; private set; } = [];
    public IReadOnlyList<PropertyCard> LatestListings { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        AllCities = await _db.Cities
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        FeaturedCities = AllCities.Where(c => c.IsFeatured).ToList();

        var citySlugByName = AllCities.ToDictionary(c => c.Name, c => c.Slug);

        // SQLite (used by tests) cannot translate ORDER BY on DateTimeOffset or aggregates on decimal,
        // so we over-fetch by Tier and finish ordering + projection in memory.
        var raw = await _db.Properties
            .AsNoTracking()
            .Include(p => p.Units)
            .Include(p => p.Images)
            .Where(p => p.Status == ListingStatus.Active)
            .OrderByDescending(p => p.Tier)
            .Take(20)
            .ToListAsync(ct);

        LatestListings = raw
            .OrderByDescending(p => p.Tier)
            .ThenByDescending(p => p.CreatedAt)
            .Take(6)
            .Select(p => new PropertyCard
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            City = p.City,
            CitySlug = citySlugByName.TryGetValue(p.City, out var slug) ? slug : p.City.ToLowerInvariant(),
            Neighbourhood = p.Neighbourhood,
            PrimaryImageUrl = p.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault(),
            FromPrice = p.Units.Count == 0 ? null : p.Units.Min(u => (decimal?)u.Price),
            MinBedrooms = p.Units.Count == 0 ? 0 : p.Units.Min(u => u.Bedrooms),
            MinBathrooms = p.Units.Count == 0 ? 0m : p.Units.Min(u => u.Bathrooms),
            PropertyType = p.PropertyType,
            Tier = p.Tier,
            IsVerified = p.IsVerified,
            PetsAllowed = p.PetsAllowed,
            Furnished = p.Furnished
        })
            .ToList();
    }
}
