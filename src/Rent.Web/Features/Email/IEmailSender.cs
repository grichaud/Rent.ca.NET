using Rent.Web.Domain;

namespace Rent.Web.Features.Email;

public interface IEmailSender
{
    Task SendInquiryToLandlordAsync(InquiryEmail data, CancellationToken ct = default);
    Task SendWelcomeAsync(WelcomeEmail data, CancellationToken ct = default);
    Task SendPasswordResetAsync(PasswordResetEmail data, CancellationToken ct = default);
    Task SendAlertDigestAsync(AlertDigestEmail data, CancellationToken ct = default);
}

public record InquiryEmail(
    string LandlordEmail,
    string LandlordName,
    string PropertyTitle,
    string PropertyUrl,
    string InboxUrl,
    string SenderName,
    string SenderEmail,
    string? SenderPhone,
    string Message,
    DateOnly? MoveInDate);

public record WelcomeEmail(
    string ToEmail,
    string ToName,
    string Role,
    string PortalUrl,
    string Locale = "en");

public record PasswordResetEmail(
    string ToEmail,
    string ToName,
    string ResetUrl,
    string Locale = "en");

/// <summary>
/// A batch of new listings matching one saved alert. Unlike the other emails, this one is
/// composed with no HTTP request in flight, so every URL must already be absolute and the
/// locale comes from the alert row rather than the ambient culture.
/// </summary>
public record AlertDigestEmail(
    string ToEmail,
    string ToName,
    string? AlertName,
    string AlertSummary,
    IReadOnlyList<AlertDigestItem> Items,
    int TotalMatches,
    string SearchUrl,
    string ManageAlertsUrl,
    string Locale = "en");

/// <summary>
/// One listing in a digest. Values stay raw so the template can format currency and
/// measurements per locale instead of receiving pre-rendered English strings.
/// </summary>
public record AlertDigestItem(
    string Title,
    string Url,
    string Location,
    decimal? MinPrice,
    decimal? MaxPrice,
    int MinBedrooms,
    int? MaxBedrooms,
    decimal MinBathrooms,
    string? ImageUrl);
