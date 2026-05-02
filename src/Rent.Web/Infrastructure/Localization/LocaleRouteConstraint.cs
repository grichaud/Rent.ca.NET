using Microsoft.AspNetCore.Routing;

namespace Rent.Web.Infrastructure.Localization;

public sealed class LocaleRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value) || value is null)
        {
            return false;
        }

        var culture = value.ToString();
        return LocalizationConfig.IsSupported(culture);
    }
}
