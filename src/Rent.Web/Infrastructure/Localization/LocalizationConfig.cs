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
}
