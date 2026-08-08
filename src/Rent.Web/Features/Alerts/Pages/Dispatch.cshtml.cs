using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Rent.Web.Features.Alerts.Engine;

namespace Rent.Web.Features.Alerts.Pages;

/// <summary>
/// Machine-to-machine trigger for the alert digest engine.
///
/// The App Service runs on the F1 tier, which does not support Always On, so the worker is
/// unloaded after a stretch without traffic and an in-process timer would fire erratically or
/// not at all. An external scheduler (GitHub Actions cron) POSTs here instead.
///
/// Authenticated by a shared secret rather than a cookie, which is why antiforgery is
/// bypassed: there is no browser session and no CSRF surface to protect — a forged
/// cross-site POST cannot supply the header, and the endpoint mutates nothing on behalf of
/// the caller's identity.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class DispatchModel : PageModel
{
    public const string TokenHeader = "X-Alerts-Token";

    /// <summary>
    /// Guards against overlapping runs (a slow run plus the next cron tick, or a manual
    /// workflow_dispatch on top of a scheduled one). Static because one process should only
    /// ever have one engine pass in flight; the F1 plan runs a single worker.
    /// </summary>
    private static readonly SemaphoreSlim RunGate = new(1, 1);

    private readonly IAlertDigestService _engine;
    private readonly AlertEngineOptions _options;
    private readonly ILogger<DispatchModel> _logger;

    public DispatchModel(
        IAlertDigestService engine,
        IOptions<AlertEngineOptions> options,
        ILogger<DispatchModel> logger)
    {
        _engine = engine;
        _options = options.Value;
        _logger = logger;
    }

    public IActionResult OnGet() => StatusCode(StatusCodes.Status405MethodNotAllowed);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Fail closed: with no secret configured the endpoint denies it exists rather than
        // running unauthenticated. A misconfigured deployment is inert, not wide open.
        if (string.IsNullOrWhiteSpace(_options.DispatchToken))
        {
            _logger.LogWarning(
                "Alert dispatch called but {Section}:{Key} is not configured; refusing.",
                AlertEngineOptions.SectionName, nameof(AlertEngineOptions.DispatchToken));
            return NotFound();
        }

        if (!IsAuthorized())
        {
            _logger.LogWarning(
                "Alert dispatch rejected: missing or invalid {Header}.", TokenHeader);
            return Unauthorized();
        }

        if (!await RunGate.WaitAsync(0, ct))
        {
            _logger.LogInformation("Alert dispatch skipped: a run is already in progress.");
            return StatusCode(StatusCodes.Status409Conflict,
                new { message = "An alert digest run is already in progress." });
        }

        try
        {
            var result = await _engine.RunAsync(ct);
            return new JsonResult(result);
        }
        finally
        {
            RunGate.Release();
        }
    }

    private bool IsAuthorized()
    {
        if (!Request.Headers.TryGetValue(TokenHeader, out var provided)) return false;

        var supplied = Encoding.UTF8.GetBytes(provided.ToString());
        var expected = Encoding.UTF8.GetBytes(_options.DispatchToken);

        // Constant-time compare so a caller cannot recover the token byte by byte from
        // response timings. Length inequality short-circuits inside FixedTimeEquals, which
        // leaks only the length — standard and acceptable for a random secret.
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
