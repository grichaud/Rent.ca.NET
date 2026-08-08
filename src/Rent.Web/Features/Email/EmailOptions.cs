namespace Rent.Web.Features.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "onboarding@resend.dev";
    public string FromName { get; set; } = "Rent.ca";
    public string BaseUrl { get; set; } = "https://api.resend.com";

    /// <summary>
    /// Demo/testing override. When set, every outgoing email is delivered to this
    /// single address instead of the real recipient, and the intended recipient is
    /// noted in the subject. Lets the app send real emails while the sender is still
    /// the Resend testing domain (onboarding@resend.dev), which only delivers to the
    /// account owner. Leave empty in production once a real domain is verified.
    /// </summary>
    public string RedirectAllTo { get; set; } = string.Empty;

    /// <summary>
    /// When set, every outgoing email is blind-copied (BCC) to this address, so the
    /// operator receives a copy of each real send for monitoring. Ignored while
    /// RedirectAllTo is active (the mail already goes to that address) and skipped when
    /// it equals the primary recipient. Requires a verified sender domain to take effect.
    /// </summary>
    public string BccAll { get; set; } = string.Empty;

    /// <summary>
    /// Absolute origin used to build links in emails composed outside an HTTP request.
    /// Request-bound emails (inquiry, welcome, password reset) get their URLs from the
    /// caller's HttpContext; the alert digest engine has no request, so it needs this.
    /// No trailing slash.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "https://rent-ca-net.azurewebsites.net";
}
