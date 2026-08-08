using Rent.Web.Domain;

namespace Rent.Web.Features.Alerts.Engine;

/// <summary>
/// Cadence rules for the digest engine. Pure functions — no I/O, no clock of their own.
/// </summary>
public static class AlertSchedule
{
    /// <summary>
    /// 23h rather than 24h. The scheduler fires hourly, so demanding a full 24 hours would
    /// let a few minutes of jitter push a daily alert into the next hour's slot and, over
    /// time, drift a "daily" digest into an every-other-day one.
    /// </summary>
    public static readonly TimeSpan DailyInterval = TimeSpan.FromHours(23);

    /// <summary>Same slack logic as <see cref="DailyInterval"/>, applied to a week.</summary>
    public static readonly TimeSpan WeeklyInterval = TimeSpan.FromDays(7) - TimeSpan.FromHours(1);

    /// <summary>
    /// Whether an alert should be evaluated on this run. Paused alerts never fire; an alert
    /// that has never been sent always does.
    /// </summary>
    public static bool IsDue(Alert alert, DateTimeOffset now)
    {
        if (!alert.IsActive) return false;
        if (alert.LastSentAt is not DateTimeOffset last) return true;

        var elapsed = now - last;
        return alert.Frequency switch
        {
            AlertFrequency.Instant => true,
            AlertFrequency.Weekly => elapsed >= WeeklyInterval,
            // Daily, plus any frequency added later: fall back to the daily cadence rather
            // than to "never", so a new enum value degrades to sending too little, not to
            // silently disabling the alert.
            _ => elapsed >= DailyInterval
        };
    }

    /// <summary>
    /// Start of the window for "new" listings. Falls back to the alert's creation time so a
    /// brand-new alert never digests the entire back catalogue.
    /// </summary>
    public static DateTimeOffset WindowStart(Alert alert) => alert.LastSentAt ?? alert.CreatedAt;
}
