using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Rent.Web.Features.Email;

public class ResendEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public Task SendInquiryToLandlordAsync(InquiryEmail data, CancellationToken ct = default)
    {
        var (subject, html) = EmailTemplates.Inquiry(data);
        return SendAsync(data.LandlordEmail, subject, html, replyTo: data.SenderEmail, ct);
    }

    public Task SendWelcomeAsync(WelcomeEmail data, CancellationToken ct = default)
    {
        var (subject, html) = EmailTemplates.Welcome(data);
        return SendAsync(data.ToEmail, subject, html, replyTo: null, ct);
    }

    public Task SendPasswordResetAsync(PasswordResetEmail data, CancellationToken ct = default)
    {
        var (subject, html) = EmailTemplates.PasswordReset(data);
        return SendAsync(data.ToEmail, subject, html, replyTo: null, ct);
    }

    private async Task SendAsync(string to, string subject, string html, string? replyTo, CancellationToken ct)
    {
        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromAddress
            : $"{_options.FromName} <{_options.FromAddress}>";

        // Demo mode: when RedirectAllTo is set, deliver to that single address (the one
        // the Resend testing domain is allowed to reach) and keep the real recipient
        // visible in the subject. See EmailOptions.RedirectAllTo.
        var recipient = to;
        string[]? bcc = null;
        if (!string.IsNullOrWhiteSpace(_options.RedirectAllTo))
        {
            recipient = _options.RedirectAllTo;
            subject = $"[demo → {to}] {subject}";
        }
        else if (!string.IsNullOrWhiteSpace(_options.BccAll)
            && !string.Equals(_options.BccAll, to, StringComparison.OrdinalIgnoreCase))
        {
            // Real send to the user, blind-copied to the operator for monitoring.
            bcc = new[] { _options.BccAll };
        }

        var payload = new ResendRequest(from, new[] { recipient }, subject, html, replyTo, bcc);

        using var response = await _http.PostAsJsonAsync("emails", payload, JsonOptions, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Resend returned {Status} for {To} ({Subject}): {Body}",
                (int)response.StatusCode, to, subject, body);
            response.EnsureSuccessStatusCode();
            return;
        }

        var parsed = JsonSerializer.Deserialize<ResendResponse>(body, JsonOptions);
        _logger.LogInformation(
            "Resend accepted {Subject} for {To} (id={Id}).", subject, to, parsed?.Id ?? "(none)");
    }

    private record ResendRequest(string From, string[] To, string Subject, string Html, string? ReplyTo, string[]? Bcc);

    private record ResendResponse(string? Id);
}
