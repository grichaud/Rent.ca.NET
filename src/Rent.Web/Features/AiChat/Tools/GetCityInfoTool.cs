using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.AiChat.Tools;

public class GetCityInfoTool : IAiTool
{
    private readonly AppDbContext _db;

    public GetCityInfoTool(AppDbContext db) => _db = db;

    public string Name => "get_city_info";

    public string Description =>
        "Look up information about a Canadian city in our database: province, listing count, slug for URLs.";

    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            city = new { type = "string", description = "City name to look up (case insensitive, partial match supported)." }
        },
        required = new[] { "city" }
    };

    public async Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson, ToolExecutionContext context, CancellationToken ct = default)
    {
        string? city = null;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("city", out var p) && p.ValueKind == JsonValueKind.String)
                city = p.GetString();
        }
        catch (JsonException)
        {
            return ToolExecutionResult.Fail(new { message = "Invalid arguments." });
        }

        if (string.IsNullOrWhiteSpace(city))
            return ToolExecutionResult.Fail(new { message = "City name is required." });

        var needle = city.Trim().ToLowerInvariant();

        var match = await _db.Cities
            .AsNoTracking()
            .Where(c => c.Name.ToLower() == needle || c.Slug == needle ||
                        c.Name.ToLower().Contains(needle))
            .OrderBy(c => c.Name.Length)
            .FirstOrDefaultAsync(ct);

        if (match is null)
        {
            return ToolExecutionResult.Fail(new { found = false, message = $"No city found matching '{city}'." });
        }

        var activeListings = await _db.Properties
            .AsNoTracking()
            .CountAsync(p => p.City == match.Name && p.Status == ListingStatus.Active, ct);

        return ToolExecutionResult.Ok(new
        {
            found = true,
            name = match.Name,
            province = match.Province,
            slug = match.Slug,
            listingCount = activeListings,
            url = $"/{match.Slug}"
        });
    }
}
