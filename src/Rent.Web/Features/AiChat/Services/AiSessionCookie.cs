namespace Rent.Web.Features.AiChat.Services;

public static class AiSessionCookie
{
    public const string Name = "rentca-aichat-session";

    public static Guid EnsureSessionId(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(Name, out var raw) &&
            Guid.TryParse(raw, out var existing))
        {
            return existing;
        }

        var fresh = Guid.NewGuid();
        context.Response.Cookies.Append(Name, fresh.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true
        });
        return fresh;
    }
}
