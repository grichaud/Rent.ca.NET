using System.Globalization;

namespace Rent.Web.Infrastructure.Localization;

public static class LocalizationConfig
{
    public const string DefaultCulture = "en";

    public static readonly string[] SupportedCultures = ["en", "fr"];

    public static readonly CultureInfo[] SupportedCultureInfos =
        SupportedCultures.Select(c => new CultureInfo(c)).ToArray();

    public static bool IsSupported(string? culture) =>
        !string.IsNullOrWhiteSpace(culture) && SupportedCultures.Contains(culture);

    /// <summary>
    /// The ambient UI culture narrowed to a supported two-letter code, falling back to the
    /// default. Used to stamp a locale onto rows that will later be rendered outside any
    /// request (e.g. alert digests sent by the background engine).
    /// </summary>
    public static string CurrentOrDefault()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return IsSupported(lang) ? lang : DefaultCulture;
    }
}
