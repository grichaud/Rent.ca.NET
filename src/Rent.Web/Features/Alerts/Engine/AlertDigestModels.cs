namespace Rent.Web.Features.Alerts.Engine;

/// <summary>
/// Tuning knobs for the digest engine. Bound from the "Alerts" configuration section.
/// </summary>
public class AlertEngineOptions
{
    public const string SectionName = "Alerts";

    /// <summary>
    /// Shared secret required by the dispatch endpoint. When empty the endpoint is not
    /// mapped at all, so a misconfigured deployment fails closed rather than open.
    /// </summary>
    public string DispatchToken { get; set; } = string.Empty;

    /// <summary>Ceiling on alerts processed in a single run (protects the F1 CPU quota).</summary>
    public int MaxAlertsPerRun { get; set; } = 200;

    /// <summary>Listings shown in one digest before it links out to the full search.</summary>
    public int MaxItemsPerEmail { get; set; } = 10;

    /// <summary>Pause between sends. Resend's free tier throttles around 2 requests/second.</summary>
    public int SendDelayMs { get; set; } = 600;
}

/// <summary>One listing that satisfied an alert, already flattened for the email template.</summary>
public record AlertMatch(
    Guid PropertyId,
    string Title,
    string Slug,
    string City,
    string Province,
    string? Neighbourhood,
    string? ImageUrl,
    decimal? MinPrice,
    decimal? MaxPrice,
    int MinBedrooms,
    int? MaxBedrooms,
    decimal MinBathrooms,
    DateTimeOffset CreatedAt);

/// <summary>Outcome of one engine run, returned by the dispatch endpoint as JSON.</summary>
public record DigestRunResult
{
    /// <summary>Active alerts examined.</summary>
    public int Considered { get; init; }

    /// <summary>Of those, how many had their cadence satisfied.</summary>
    public int Due { get; init; }

    /// <summary>Digests actually delivered.</summary>
    public int Sent { get; init; }

    /// <summary>Due alerts with no new listings — deliberately left unstamped.</summary>
    public int NoMatches { get; init; }

    /// <summary>Sends that threw. These keep their old LastSentAt and retry next run.</summary>
    public int Failed { get; init; }

    /// <summary>Total listings across all delivered digests.</summary>
    public int PropertiesIncluded { get; init; }

    public long ElapsedMs { get; init; }
}
