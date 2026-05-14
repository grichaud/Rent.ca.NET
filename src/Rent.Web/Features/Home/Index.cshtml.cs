using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Favorites;
using Rent.Web.Features.Search;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Home;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFavoriteService _favorites;

    public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager, IFavoriteService favorites)
    {
        _db = db;
        _userManager = userManager;
        _favorites = favorites;
    }

    public IReadOnlyList<City> FeaturedCities { get; private set; } = [];
    public IReadOnlyList<PropertyCard> LatestListings { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var allCities = await _db.Cities
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var featuredOrder = new[]
        {
            "toronto", "vancouver", "montreal", "calgary", "edmonton", "ottawa",
            "winnipeg", "quebec-city", "hamilton", "saskatoon", "london", "halifax"
        };
        var featuredBySlug = allCities.Where(c => c.IsFeatured).ToDictionary(c => c.Slug);
        FeaturedCities = featuredOrder
            .Where(featuredBySlug.ContainsKey)
            .Select(slug => featuredBySlug[slug])
            .ToList();

        var citySlugByName = allCities.ToDictionary(c => c.Name, c => c.Slug);

        // SQLite (used by tests) cannot translate ORDER BY on DateTimeOffset or aggregates on decimal,
        // so we over-fetch by Tier and finish ordering + projection in memory.
        var raw = await _db.Properties
            .AsNoTracking()
            .Include(p => p.Units)
            .Include(p => p.Images)
            .Include(p => p.RentSpecials)
            .Where(p => p.Status == ListingStatus.Active)
            .OrderByDescending(p => p.Tier)
            .Take(20)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;

        LatestListings = raw
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
                TierExpiresAt = p.TierExpiresAt,
                IsVerified = p.IsVerified,
                PetsAllowed = p.PetsAllowed,
                Furnished = p.Furnished,
                CreatedAt = p.CreatedAt,
                SpecialTitle = p.ActiveSpecial(now)?.Title
            })
            .OrderByDescending(c => c.EffectiveTier)
            .ThenByDescending(c => c.CreatedAt)
            .Take(6)
            .ToList();

        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(_userManager.GetUserId(User), out var uid))
        {
            var favIds = await _favorites.GetFavoritedIdsAsync(uid, LatestListings.Select(c => c.Id), ct);
            foreach (var card in LatestListings)
                card.IsFavorited = favIds.Contains(card.Id);
        }
    }
}
