using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Localization.Pages;

public class SetLanguageModel : PageModel
{
    public IActionResult OnGet() => RedirectToHome();

    public IActionResult OnPost(string? culture, string? returnUrl)
    {
        var target = LocalizationConfig.IsSupported(culture)
            ? culture!
            : LocalizationConfig.DefaultCulture;

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(target)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

        var safeReturnUrl = NormalizeReturnUrl(returnUrl, target);
        return LocalRedirect(safeReturnUrl);
    }

    private IActionResult RedirectToHome() =>
        LocalRedirect("/" + LocalizationConfig.DefaultCulture);

    public static string NormalizeReturnUrl(string? returnUrl, string targetCulture)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
        {
            return "/" + targetCulture;
        }

        var pathStart = returnUrl;
        var queryStart = returnUrl.IndexOf('?');
        var query = string.Empty;
        if (queryStart >= 0)
        {
            pathStart = returnUrl[..queryStart];
            query = returnUrl[queryStart..];
        }

        if (pathStart == "/")
        {
            return "/" + targetCulture + query;
        }

        var trimmed = pathStart.TrimStart('/');
        var firstSlash = trimmed.IndexOf('/');
        var firstSegment = firstSlash < 0 ? trimmed : trimmed[..firstSlash];
        var rest = firstSlash < 0 ? string.Empty : trimmed[firstSlash..];

        if (LocalizationConfig.IsSupported(firstSegment))
        {
            return "/" + targetCulture + rest + query;
        }

        return "/" + targetCulture + pathStart + query;
    }
}
