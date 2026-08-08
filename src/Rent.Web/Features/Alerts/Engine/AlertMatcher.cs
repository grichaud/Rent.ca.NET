using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Alerts.Engine;

/// <summary>
/// Finds listings published since an alert was last serviced that satisfy its criteria.
///
/// Deliberately does NOT reuse <see cref="Features.Search.SearchHandler"/>: that handler
/// records every query into PopularSearches for the admin dashboard, so driving it from an
/// hourly engine would fabricate one phantom search per alert per run. The filter semantics
/// below mirror it on purpose (price/bed/bath are evaluated against Units, not the property),
/// and the two must be kept in step.
/// </summary>
public class AlertMatcher
{
    private readonly AppDbContext _db;

    public AlertMatcher(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertMatch>> FindNewMatchesAsync(
        Alert alert, DateTimeOffset since, int max, CancellationToken ct = default)
    {
        if (max <= 0) return Array.Empty<AlertMatch>();

        // NOTE: the `since` cutoff is applied in memory below, not here. EF Core's SQLite
        // provider (the test fixture) cannot translate a DateTimeOffset comparison at all —
        // not just ORDER BY — so `.Where(p => p.CreatedAt > since)` throws at query time.
        // AiChatService.GetActiveConversationAsync hits the same wall and resolves it the
        // same way. Every other filter stays in SQL, so what comes back is already narrowed
        // by city, type, price, beds, baths and pets; only the date window is client-side.
        var q = _db.Properties
            .AsNoTracking()
            .Where(p => p.Status == ListingStatus.Active);

        if (!string.IsNullOrWhiteSpace(alert.City))
        {
            // Alert.City holds the city NAME (both the form's <option value> and the AI tool
            // store it that way), matching Property.City. SQL Server's default collation is
            // case-insensitive, so "toronto" from the AI tool still matches "Toronto".
            var city = alert.City.Trim();
            q = q.Where(p => p.City == city);
        }

        if (alert.PropertyType is PropertyType type)
            q = q.Where(p => p.PropertyType == type);

        if (alert.PriceMin is decimal priceMin)
            q = q.Where(p => p.Units.Any(u => u.Price >= priceMin));

        if (alert.PriceMax is decimal priceMax)
            q = q.Where(p => p.Units.Any(u => u.Price <= priceMax));

        if (alert.BedroomsMin is int bedrooms)
            q = q.Where(p => p.Units.Any(u => u.Bedrooms >= bedrooms));

        if (alert.BathroomsMin is decimal bathrooms)
            q = q.Where(p => p.Units.Any(u => u.Bathrooms >= bathrooms));

        if (alert.PetsAllowed is bool pets)
            q = q.Where(p => p.PetsAllowed == pets);

        // Units come back as a list and the Min/Max run in memory: SQLite cannot translate
        // decimal aggregates either. Same reason SearchHandler projects this way.
        var raw = await q
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.City,
                p.Province,
                p.Neighbourhood,
                p.CreatedAt,
                ImageUrl = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault(),
                Units = p.Units.Select(u => new { u.Price, u.Bedrooms, u.Bathrooms }).ToList()
            })
            .ToListAsync(ct);

        // Date window + newest-first ordering, both client-side for the reason above.
        // Scale note: this materialises every active listing matching the non-date filters.
        // At this catalogue's size that is a few dozen rows; if a single city ever holds
        // thousands of active listings, revisit by giving Property a DateTime (not
        // DateTimeOffset) shadow column that SQLite can compare, and push this into SQL.
        return raw
            .Where(p => p.CreatedAt > since)
            .OrderByDescending(p => p.CreatedAt)
            .Take(max)
            .Select(p => new AlertMatch(
                p.Id,
                p.Title,
                p.Slug,
                p.City,
                p.Province,
                p.Neighbourhood,
                p.ImageUrl,
                p.Units.Count == 0 ? null : p.Units.Min(u => (decimal?)u.Price),
                p.Units.Count == 0 ? null : p.Units.Max(u => (decimal?)u.Price),
                p.Units.Count == 0 ? 0 : p.Units.Min(u => u.Bedrooms),
                p.Units.Count == 0 ? null : p.Units.Max(u => (int?)u.Bedrooms),
                p.Units.Count == 0 ? 0m : p.Units.Min(u => u.Bathrooms),
                p.CreatedAt))
            .ToList();
    }
}
