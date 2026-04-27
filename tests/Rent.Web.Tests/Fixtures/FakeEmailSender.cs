using Rent.Web.Features.Email;

namespace Rent.Web.Tests.Fixtures;

public class FakeEmailSender : IEmailSender
{
    public List<InquiryEmail> Inquiries { get; } = new();
    public List<WelcomeEmail> Welcomes { get; } = new();
    public List<PasswordResetEmail> PasswordResets { get; } = new();

    public bool ShouldThrow { get; set; }

    public void Reset()
    {
        Inquiries.Clear();
        Welcomes.Clear();
        PasswordResets.Clear();
        ShouldThrow = false;
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
}
