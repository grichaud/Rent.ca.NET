using System.Globalization;

namespace Rent.Web.Features.Shared;

/// <summary>
/// Fechas largas con orden correcto por idioma: "Jul 15, 2026" (EN) vs "15 juil. 2026" (FR).
/// El patron "MMM d, yyyy" produce "juil. 15, 2026" en frances, que no es orden valido.
/// </summary>
public static class DateFormat
{
    public static string Long(DateTimeOffset value) => Long(value.Date);

    public static string Long(DateOnly value) => Long(value.ToDateTime(TimeOnly.MinValue));

    private static string Long(DateTime value)
    {
        var culture = CultureInfo.CurrentUICulture;
        var pattern = culture.TwoLetterISOLanguageName == "fr" ? "d MMM yyyy" : "MMM d, yyyy";
        return value.ToString(pattern, culture);
    }
}
