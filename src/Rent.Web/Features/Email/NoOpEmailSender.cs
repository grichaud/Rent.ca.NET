namespace Rent.Web.Features.Email;

public class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendInquiryToLandlordAsync(InquiryEmail data, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOp] Inquiry email skipped (no API key). To={Email} Property={Title}",
            data.LandlordEmail, data.PropertyTitle);
        return Task.CompletedTask;
    }

    public Task SendWelcomeAsync(WelcomeEmail data, CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOp] Welcome email skipped (no API key). To={Email}", data.ToEmail);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(PasswordResetEmail data, CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOp] Password-reset email skipped (no API key). To={Email}", data.ToEmail);
        return Task.CompletedTask;
    }
}
