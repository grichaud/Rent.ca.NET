using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http.Extensions;

namespace Rent.Web.Infrastructure.Localization;

public sealed class PathLocaleRedirectMiddleware
{
    private static readonly string[] PassthroughPrefixes =
    [
        "/api/",
        "/health",
        "/Error",
        "/error",
        "/inquiries/submit",
        "/renter/favorites/toggle",
        "/logout",
        "/signin-google",
        "/signin-",
        "/external-login-callback",
        "/set-language",
        "/lib/",
        "/css/",
        "/js/",
        "/img/",
        "/favicon",
        "/icon.svg",
        "/_framework/",
        "/_vs/",
    ];

    private readonly RequestDelegate _next;

    public PathLocaleRedirectMiddleware(RequestDelegate next)
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

        var firstSegment = ExtractFirstSegment(path);
        if (LocalizationConfig.IsSupported(firstSegment))
        {
            await _next(context);
            return;
        }

        var locale = ResolveLocale(context);
        var newPath = path == "/"
            ? "/" + locale
            : "/" + locale + path;

        var queryString = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;
        var redirectUrl = newPath + queryString;

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = redirectUrl;
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

    private static string? ExtractFirstSegment(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return null;
        }

        var trimmed = path.AsSpan(1);
        var slashIndex = trimmed.IndexOf('/');
        return slashIndex < 0 ? trimmed.ToString() : trimmed.Slice(0, slashIndex).ToString();
    }

    private static string ResolveLocale(HttpContext context)
    {
        var cookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
        if (!string.IsNullOrEmpty(cookie))
        {
            var parsed = CookieRequestCultureProvider.ParseCookieValue(cookie);
            var culture = parsed?.UICultures.FirstOrDefault().Value ?? parsed?.Cultures.FirstOrDefault().Value;
            if (LocalizationConfig.IsSupported(culture))
            {
                return culture!;
            }
        }

        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            foreach (var token in acceptLanguage.Split(','))
            {
                var tag = token.Split(';')[0].Trim();
                if (tag.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
                {
                    return "fr";
                }
                if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    return "en";
                }
            }
        }

        return LocalizationConfig.DefaultCulture;
    }
}
