using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Search;

public class SearchHandler
{
    private readonly AppDbContext _db;

    public SearchHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SearchResult> ExecuteAsync(SearchQuery query, CancellationToken ct = default)
    {
        var citySlug = query.CitySlug?.ToLowerInvariant() ?? string.Empty;

        var city = await _db.Cities
            .FirstOrDefaultAsync(c => c.Slug == citySlug, ct);

        if (city is null)
        {
            return new SearchResult { City = null };
        }

        var q = _db.Properties
            .AsNoTracking()
            .Where(p => p.Status == ListingStatus.Active && p.City == city.Name);

        if (query.MinPrice.HasValue)
            q = q.Where(p => p.Units.Any(u => u.Price >= query.MinPrice));
        if (query.MaxPrice.HasValue)
            q = q.Where(p => p.Units.Any(u => u.Price <= query.MaxPrice));
        if (query.Bedrooms.HasValue)
            q = q.Where(p => p.Units.Any(u => u.Bedrooms >= query.Bedrooms));
        if (query.Type.HasValue)
            q = q.Where(p => p.PropertyType == query.Type);
        if (query.PetsAllowed)
            q = q.Where(p => p.PetsAllowed);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(p => p.Tier)
            .ThenByDescending(p => p.IsVerified)
            .ThenBy(p => p.Title)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new PropertyCard
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                City = p.City,
                CitySlug = city.Slug,
                Neighbourhood = p.Neighbourhood,
                PrimaryImageUrl = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault(),
                FromPrice = p.Units.Min(u => (decimal?)u.Price),
                MinBedrooms = p.Units.Min(u => (int?)u.Bedrooms) ?? 0,
                MinBathrooms = p.Units.Min(u => (decimal?)u.Bathrooms) ?? 0m,
                PropertyType = p.PropertyType,
                Tier = p.Tier,
                IsVerified = p.IsVerified,
                PetsAllowed = p.PetsAllowed,
                Furnished = p.Furnished
            })
            .ToListAsync(ct);

        return new SearchResult
        {
            City = city,
            Properties = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
