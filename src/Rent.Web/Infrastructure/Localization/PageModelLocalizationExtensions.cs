using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Infrastructure.Localization;

public static class PageModelLocalizationExtensions
{
    public static string CurrentCulture(this PageModel page)
    {
        return ResolveCulture(page.HttpContext);
    }

    public static string Localized(this PageModel page, string path)
    {
        var culture = ResolveCulture(page.HttpContext);
        return Localize(culture, path);
    }

    public static string Localize(string culture, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/" + culture;
        }

        if (!path.StartsWith('/'))
        {
            return path;
        }

        if (path == "/")
        {
            return "/" + culture;
        }

        var trimmed = path.AsSpan(1);
        var slashIndex = trimmed.IndexOf('/');
        var firstSegment = slashIndex < 0 ? trimmed.ToString() : trimmed[..slashIndex].ToString();

        if (LocalizationConfig.IsSupported(firstSegment))
        {
            return path;
        }

        return "/" + culture + path;
    }

    public static string ResolveCulture(HttpContext httpContext)
    {
        var routeCulture = httpContext.GetRouteValue("culture") as string;
        if (LocalizationConfig.IsSupported(routeCulture))
        {
            return routeCulture!;
        }

        var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return LocalizationConfig.IsSupported(ui) ? ui : LocalizationConfig.DefaultCulture;
    }
}
