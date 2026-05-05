using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.AiChat.Tools;

public class SearchPropertiesTool : IAiTool
{
    private readonly AppDbContext _db;

    public SearchPropertiesTool(AppDbContext db) => _db = db;

    public string Name => "search_properties";

    public string Description =>
        "Search rental properties by city, type, price, bedrooms, and pet policy. Returns up to 5 matching active listings.";

    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            city = new { type = "string", description = "City name (e.g. 'Toronto', 'Vancouver'). Optional." },
            property_type = new
            {
                type = "string",
                description = "Property type. Optional.",
                @enum = new[] { "apartment", "condo", "house", "townhouse", "basement", "studio", "loft", "duplex", "other" }
            },
            price_min = new { type = "number", description = "Minimum monthly rent in CAD. Optional." },
            price_max = new { type = "number", description = "Maximum monthly rent in CAD. Optional." },
            bedrooms = new { type = "integer", description = "Minimum bedrooms (0=studio). Optional." },
            pets_allowed = new { type = "boolean", description = "Pets allowed. Optional." }
        },
        required = Array.Empty<string>()
    };

    public async Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson, ToolExecutionContext context, CancellationToken ct = default)
    {
        var args = ParseArgs(argumentsJson);

        var q = _db.Properties
            .AsNoTracking()
            .Where(p => p.Status == ListingStatus.Active);

        if (!string.IsNullOrWhiteSpace(args.City))
        {
            var city = args.City.Trim();
            q = q.Where(p => p.City == city);
        }
        if (args.PropertyType is { } pt)
            q = q.Where(p => p.PropertyType == pt);
        if (args.PriceMin is { } pmin)
            q = q.Where(p => p.Units.Any(u => u.Price >= pmin));
        if (args.PriceMax is { } pmax)
            q = q.Where(p => p.Units.Any(u => u.Price <= pmax));
        if (args.Bedrooms is { } bed)
            q = q.Where(p => p.Units.Any(u => u.Bedrooms >= bed));
        if (args.PetsAllowed == true)
            q = q.Where(p => p.PetsAllowed);

        // Over-fetch by raw Tier in SQL, then re-rank by EffectiveTier in memory so listings whose
        // tier expired don't get an artificial boost in the AI tool ranking.
        var rawCandidates = await q
            .OrderByDescending(p => p.Tier)
            .Take(20)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.City,
                p.PropertyType,
                p.Tier,
                p.TierExpiresAt,
                p.IsVerified,
                Units = p.Units.Select(u => new { u.Bedrooms, u.Bathrooms, u.Price }).ToList()
            })
            .ToListAsync(ct);

        var raw = rawCandidates
            .OrderByDescending(p => ListingTierExtensions.Resolve(p.Tier, p.TierExpiresAt))
            .ThenByDescending(p => p.IsVerified)
            .ThenBy(p => p.Title)
            .Take(5)
            .ToList();

        var slugs = raw.Select(r => r.City).Distinct().ToList();
        var cityMap = await _db.Cities
            .AsNoTracking()
            .Where(c => slugs.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, c => c.Slug, ct);

        var results = raw.Select(p =>
        {
            var minPrice = p.Units.Count > 0 ? p.Units.Min(u => (decimal?)u.Price) : null;
            var minBeds = p.Units.Count > 0 ? p.Units.Min(u => (int?)u.Bedrooms) : null;
            var citySlug = cityMap.TryGetValue(p.City, out var s) ? s : p.City.ToLowerInvariant();
            return new
            {
                id = p.Id,
                title = p.Title,
                city = p.City,
                propertyType = p.PropertyType.ToString(),
                fromPrice = minPrice,
                minBedrooms = minBeds,
                url = $"/{citySlug}/{p.Slug}"
            };
        }).ToList();

        return ToolExecutionResult.Ok(new { count = results.Count, properties = results });
    }

    private static SearchArgs ParseArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new SearchArgs();
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var args = new SearchArgs
            {
                City = TryGetString(root, "city"),
                PriceMin = TryGetDecimal(root, "price_min"),
                PriceMax = TryGetDecimal(root, "price_max"),
                Bedrooms = TryGetInt(root, "bedrooms"),
                PetsAllowed = TryGetBool(root, "pets_allowed")
            };
            var typeStr = TryGetString(root, "property_type");
            if (!string.IsNullOrWhiteSpace(typeStr) &&
                Enum.TryParse<PropertyType>(typeStr, ignoreCase: true, out var pt))
            {
                args.PropertyType = pt;
            }
            return args;
        }
        catch (JsonException)
        {
            return new SearchArgs();
        }
    }

    private static string? TryGetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static decimal? TryGetDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
    private static int? TryGetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
    private static bool? TryGetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private class SearchArgs
    {
        public string? City { get; set; }
        public PropertyType? PropertyType { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
        public int? Bedrooms { get; set; }
        public bool? PetsAllowed { get; set; }
    }
}
