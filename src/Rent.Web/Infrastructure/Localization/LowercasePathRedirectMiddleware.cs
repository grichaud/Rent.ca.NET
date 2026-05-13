namespace Rent.Web.Infrastructure.Localization;

/// <summary>
/// 301 redirect to lowercase for human-facing URLs.
/// Runs before <see cref="PathLocaleRedirectMiddleware"/> so the locale prefix is
/// already lowercase (e.g. `/EN/Toronto` redirects in one hop to `/en/toronto`).
/// </summary>
public sealed class LowercasePathRedirectMiddleware
{
    // Routes whose path is allowed to keep mixed case (framework/static/API endpoints
    // and case-sensitive file storage). Compared with OrdinalIgnoreCase so both
    // `/Error` and `/error` pass through.
    private static readonly string[] PassthroughPrefixes =
    [
        "/api/",
        "/health",
        "/error",
        "/inquiries/submit",
        "/renter/favorites/toggle",
        "/logout",
        "/signin-",
        "/external-login-callback",
        "/set-language",
        "/lib/",
        "/css/",
        "/js/",
        "/img/",
        "/images/",
        "/favicon",
        "/icon.svg",
        "/_framework/",
        "/_vs/",
        "/_blazor",
    ];

    private readonly RequestDelegate _next;

    public LowercasePathRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            await _next(context);
            return;
        }

        var path = request.Path.HasValue ? request.Path.Value! : "/";

        if (IsPassthrough(path))
        {
            await _next(context);
            return;
        }

        var lowered = path.ToLowerInvariant();
        if (string.Equals(path, lowered, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        var queryString = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = lowered + queryString;
    }

    private static bool IsPassthrough(string path)
    {
        foreach (var prefix in PassthroughPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
