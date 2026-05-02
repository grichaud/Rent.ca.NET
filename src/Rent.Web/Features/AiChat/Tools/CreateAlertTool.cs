using System.Text.Json;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.AiChat.Tools;

public class CreateAlertTool : IAiTool
{
    private readonly AppDbContext _db;

    public CreateAlertTool(AppDbContext db) => _db = db;

    public string Name => "create_alert";

    public string Description =>
        "Create an email alert for new rental listings matching the given criteria. Requires a logged-in user.";

    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            city = new { type = "string", description = "City name. Required." },
            property_type = new
            {
                type = "string",
                description = "Property type. Optional.",
                @enum = new[] { "apartment", "condo", "house", "townhouse", "basement", "studio", "loft", "duplex", "other" }
            },
            price_max = new { type = "number", description = "Maximum monthly rent in CAD. Optional." },
            bedrooms_min = new { type = "integer", description = "Minimum bedrooms. Optional." },
            pets_allowed = new { type = "boolean", description = "Require pets allowed. Optional." }
        },
        required = new[] { "city" }
    };

    public async Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson, ToolExecutionContext context, CancellationToken ct = default)
    {
        if (context.UserId is not Guid userId)
        {
            return ToolExecutionResult.Fail(new
            {
                requiresLogin = true,
                message = "Sign in or create an account to receive alerts. Once signed in, ask me again and I'll set it up."
            });
        }

        AlertArgs args;
        try { args = ParseArgs(argumentsJson); }
        catch (JsonException) { return ToolExecutionResult.Fail(new { message = "Invalid arguments." }); }

        if (string.IsNullOrWhiteSpace(args.City))
            return ToolExecutionResult.Fail(new { message = "City is required to create an alert." });

        var alert = new Alert
        {
            UserId = userId,
            City = args.City.Trim(),
            PropertyType = args.PropertyType,
            PriceMax = args.PriceMax,
            BedroomsMin = args.BedroomsMin,
            PetsAllowed = args.PetsAllowed,
            Frequency = AlertFrequency.Daily,
            IsActive = true
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);

        var summary = BuildSummary(alert);
        return ToolExecutionResult.Ok(new
        {
            alertId = alert.Id,
            summary,
            frequency = alert.Frequency.ToString(),
            manageUrl = "/renter/alerts"
        });
    }

    private static string BuildSummary(Alert a)
    {
        var parts = new List<string>();
        if (a.PropertyType is { } pt) parts.Add(pt.ToString().ToLowerInvariant());
        else parts.Add("rentals");
        parts.Add($"in {a.City}");
        if (a.PriceMax is { } pmax) parts.Add($"under ${pmax:N0}");
        if (a.BedroomsMin is { } bed) parts.Add($"{bed}+ bedrooms");
        if (a.PetsAllowed == true) parts.Add("pet-friendly");
        return string.Join(" ", parts);
    }

    private static AlertArgs ParseArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new AlertArgs();
        using var doc = JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;
        var args = new AlertArgs
        {
            City = root.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
            PriceMax = root.TryGetProperty("price_max", out var pmax) && pmax.ValueKind == JsonValueKind.Number ? pmax.GetDecimal() : null,
            BedroomsMin = root.TryGetProperty("bedrooms_min", out var bed) && bed.ValueKind == JsonValueKind.Number ? bed.GetInt32() : null,
            PetsAllowed = root.TryGetProperty("pets_allowed", out var p) ? p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => (bool?)null
            } : null
        };
        if (root.TryGetProperty("property_type", out var t) && t.ValueKind == JsonValueKind.String &&
            Enum.TryParse<PropertyType>(t.GetString(), ignoreCase: true, out var pt))
        {
            args.PropertyType = pt;
        }
        return args;
    }

    private class AlertArgs
    {
        public string? City { get; set; }
        public PropertyType? PropertyType { get; set; }
        public decimal? PriceMax { get; set; }
        public int? BedroomsMin { get; set; }
        public bool? PetsAllowed { get; set; }
    }
}
