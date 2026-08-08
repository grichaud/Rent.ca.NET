namespace Rent.Web.Features.Alerts.Engine;

/// <summary>
/// The alert engine. Kept behind an interface because the trigger is deliberately swappable:
/// today an external cron POSTs to the dispatch endpoint (the App Service runs on F1, which
/// does not support Always On, so an in-process timer would not fire reliably), but nothing
/// here assumes that.
/// </summary>
public interface IAlertDigestService
{
    Task<DigestRunResult> RunAsync(CancellationToken ct = default);
}
