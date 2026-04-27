using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Rent.Web.Tests.Fixtures;

/// <summary>
/// Test-only IAntiforgery that bypasses CSRF validation so HTTP-level integration
/// tests can POST without first scraping a token. Production keeps the real validator.
/// </summary>
public class NoOpAntiforgery : IAntiforgery
{
    private static readonly AntiforgeryTokenSet EmptyTokens = new("token", "cookieToken", "fieldName", "headerName");

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => EmptyTokens;
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => EmptyTokens;
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
}
