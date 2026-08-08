using Rent.Web.Features.Email;

namespace Rent.Web.Tests.Fixtures;

public class FakeEmailSender : IEmailSender
{
    public List<InquiryEmail> Inquiries { get; } = new();
    public List<WelcomeEmail> Welcomes { get; } = new();
    public List<PasswordResetEmail> PasswordResets { get; } = new();
    public List<AlertDigestEmail> AlertDigests { get; } = new();

    public bool ShouldThrow { get; set; }

    /// <summary>
    /// When set, only digests addressed to this recipient throw. Lets a test assert that one
    /// failing send does not abort the run or stamp LastSentAt, while its neighbours succeed.
    /// </summary>
    public string? ThrowForDigestTo { get; set; }

    public void Reset()
    {
        Inquiries.Clear();
        Welcomes.Clear();
        PasswordResets.Clear();
        AlertDigests.Clear();
        ShouldThrow = false;
        ThrowForDigestTo = null;
    }

    public Task SendInquiryToLandlordAsync(InquiryEmail data, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated email failure.");
        Inquiries.Add(data);
        return Task.CompletedTask;
    }

    public Task SendWelcomeAsync(WelcomeEmail data, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated email failure.");
        Welcomes.Add(data);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(PasswordResetEmail data, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated email failure.");
        PasswordResets.Add(data);
        return Task.CompletedTask;
    }

    public Task SendAlertDigestAsync(AlertDigestEmail data, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated email failure.");
        if (ThrowForDigestTo is not null &&
            string.Equals(ThrowForDigestTo, data.ToEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated digest failure for {data.ToEmail}.");
        }

        AlertDigests.Add(data);
        return Task.CompletedTask;
    }
}
