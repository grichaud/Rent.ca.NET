using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rent.Web.Domain;
using Rent.Web.Features.Email;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Alerts.Engine;

public class AlertDigestService : IAlertDigestService
{
    /// <summary>
    /// Upper bound on listings counted for a single alert. Only affects the "+N more" figure
    /// in the email — the digest itself shows at most MaxItemsPerEmail. Stops a long-dormant
    /// alert from materialising an unbounded result set just to print a number.
    /// </summary>
    private const int CountCap = 100;

    private readonly AppDbContext _db;
    private readonly AlertMatcher _matcher;
    private readonly IEmailSender _email;
    private readonly AlertEngineOptions _options;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AlertDigestService> _logger;

    public AlertDigestService(
        AppDbContext db,
        AlertMatcher matcher,
        IEmailSender email,
        IOptions<AlertEngineOptions> options,
        IOptions<EmailOptions> emailOptions,
        ILogger<AlertDigestService> logger)
    {
        _db = db;
        _matcher = matcher;
        _email = email;
        _options = options.Value;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<DigestRunResult> RunAsync(CancellationToken ct = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var now = DateTimeOffset.UtcNow;

        var active = await _db.Alerts
            .Include(a => a.User)
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        // Cadence check and ordering both happen in memory: they read LastSentAt, and SQLite
        // (the test fixture) cannot compare or sort a DateTimeOffset column.
        // Oldest-serviced first, so a run that hits MaxAlertsPerRun never starves the same
        // alerts twice — never-sent alerts sort to the front.
        var due = active
            .Where(a => AlertSchedule.IsDue(a, now))
            .OrderBy(a => a.LastSentAt ?? DateTimeOffset.MinValue)
            .Take(_options.MaxAlertsPerRun)
            .ToList();

        if (due.Count < active.Count(a => AlertSchedule.IsDue(a, now)))
        {
            _logger.LogWarning(
                "Alert engine capped this run at {Cap} alerts; the remainder will be picked up next run.",
                _options.MaxAlertsPerRun);
        }

        var citySlugs = await LoadCitySlugsAsync(ct);

        int sent = 0, noMatches = 0, failed = 0, included = 0;

        foreach (var alert in due)
        {
            ct.ThrowIfCancellationRequested();

            var recipient = alert.User?.Email;
            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogWarning("Alert {AlertId} has no deliverable email; skipping.", alert.Id);
                failed++;
                continue;
            }

            try
            {
                var since = AlertSchedule.WindowStart(alert);
                var matches = await _matcher.FindNewMatchesAsync(alert, since, CountCap, ct);

                // A listing whose city has no Cities row cannot produce a working detail URL
                // (the page 404s unless citySlug resolves and matches p.City), so it is
                // dropped rather than emailed as a dead link.
                var linkable = matches
                    .Where(m => citySlugs.ContainsKey(m.City))
                    .ToList();

                if (linkable.Count == 0)
                {
                    noMatches++;
                    continue;
                }

                var locale = LocalizationConfig.IsSupported(alert.Locale)
                    ? alert.Locale
                    : LocalizationConfig.DefaultCulture;

                var shown = linkable.Take(_options.MaxItemsPerEmail).ToList();
                var citySlug = citySlugs[shown[0].City];

                await _email.SendAlertDigestAsync(new AlertDigestEmail(
                    ToEmail: recipient,
                    ToName: alert.User?.FullName ?? string.Empty,
                    AlertName: alert.Name,
                    AlertSummary: alert.City ?? shown[0].City,
                    Items: shown.Select(m => new AlertDigestItem(
                        Title: m.Title,
                        Url: ListingUrl(locale, citySlugs[m.City], m.Slug),
                        Location: string.IsNullOrWhiteSpace(m.Neighbourhood)
                            ? m.City
                            : $"{m.Neighbourhood}, {m.City}",
                        MinPrice: m.MinPrice,
                        MaxPrice: m.MaxPrice,
                        MinBedrooms: m.MinBedrooms,
                        MaxBedrooms: m.MaxBedrooms,
                        MinBathrooms: m.MinBathrooms,
                        ImageUrl: m.ImageUrl)).ToList(),
                    TotalMatches: linkable.Count,
                    SearchUrl: SearchUrl(locale, citySlug),
                    ManageAlertsUrl: ManageAlertsUrl(locale),
                    Locale: locale), ct);

                // Stamped only after the send succeeded. A run that found nothing leaves
                // LastSentAt alone on purpose: the window must keep growing so a listing
                // published today is still eligible next run.
                alert.LastSentAt = now;
                alert.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);

                sent++;
                included += shown.Count;

                if (_options.SendDelayMs > 0)
                    await Task.Delay(_options.SendDelayMs, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad alert must not abort the batch. LastSentAt stays untouched, so this
                // alert is simply retried on the next run.
                _logger.LogError(ex, "Alert {AlertId} digest failed; will retry next run.", alert.Id);
                failed++;
            }
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        _logger.LogInformation(
            "Alert engine finished: considered={Considered} due={Due} sent={Sent} noMatches={NoMatches} failed={Failed} in {Ms}ms.",
            active.Count, due.Count, sent, noMatches, failed, (long)elapsed.TotalMilliseconds);

        return new DigestRunResult
        {
            Considered = active.Count,
            Due = due.Count,
            Sent = sent,
            NoMatches = noMatches,
            Failed = failed,
            PropertiesIncluded = included,
            ElapsedMs = (long)elapsed.TotalMilliseconds
        };
    }

    private async Task<Dictionary<string, string>> LoadCitySlugsAsync(CancellationToken ct) =>
        await _db.Cities
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Name, c => c.Slug, StringComparer.OrdinalIgnoreCase, ct);

    private string BaseUrl => _emailOptions.PublicBaseUrl.TrimEnd('/');

    private string ListingUrl(string locale, string citySlug, string propertySlug) =>
        $"{BaseUrl}/{locale}/{citySlug}/{propertySlug}";

    private string SearchUrl(string locale, string citySlug) =>
        $"{BaseUrl}/{locale}/{citySlug}";

    private string ManageAlertsUrl(string locale) =>
        $"{BaseUrl}/{locale}/renter/alerts";
}
