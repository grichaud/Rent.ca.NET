using Rent.Web.Domain;

namespace Rent.Web.Features.Email;

public interface IEmailSender
{
    Task SendInquiryToLandlordAsync(InquiryEmail data, CancellationToken ct = default);
    Task SendWelcomeAsync(WelcomeEmail data, CancellationToken ct = default);
    Task SendPasswordResetAsync(PasswordResetEmail data, CancellationToken ct = default);
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
