using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rent.Web.Features.Email;

namespace Rent.Web.Tests.Features;

public class ResendEmailSenderTests
{
    /// <summary>Captures the JSON body Resend would receive, and returns 200.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public JsonElement? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var json = await request.Content!.ReadAsStringAsync(ct);
            Body = JsonSerializer.Deserialize<JsonElement>(json);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"test-id\"}")
            };
        }
    }

    private static (ResendEmailSender sender, CapturingHandler handler) Build(EmailOptions options)
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var sender = new ResendEmailSender(
            http, Options.Create(options), NullLogger<ResendEmailSender>.Instance);
        return (sender, handler);
    }

    private static WelcomeEmail SampleWelcome() =>
        new("ana.renter@example.com", "Ana", "Renter", "https://localhost/en/renter", "en");

    [Fact]
    public async Task Send_WithoutRedirect_DeliversToRealRecipient()
    {
        var (sender, handler) = Build(new EmailOptions { ApiKey = "test" });

        await sender.SendWelcomeAsync(SampleWelcome());

        var to = handler.Body!.Value.GetProperty("to")[0].GetString();
        to.Should().Be("ana.renter@example.com");
        handler.Body!.Value.GetProperty("subject").GetString().Should().NotContain("[demo");
    }

    [Fact]
    public async Task Send_WithRedirect_DeliversToOwnerAndNotesRealRecipient()
    {
        var (sender, handler) = Build(new EmailOptions
        {
            ApiKey = "test",
            RedirectAllTo = "owner@gmail.com"
        });

        await sender.SendWelcomeAsync(SampleWelcome());

        var to = handler.Body!.Value.GetProperty("to")[0].GetString();
        to.Should().Be("owner@gmail.com");
        // The real recipient stays visible in the subject.
        handler.Body!.Value.GetProperty("subject").GetString()
            .Should().Contain("[demo → ana.renter@example.com]");
    }
}
