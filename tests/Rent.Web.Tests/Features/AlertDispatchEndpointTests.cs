using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Rent.Web.Features.Alerts.Pages;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AlertDispatchEndpointTests : IClassFixture<RentAppFactory>
{
    private const string Url = "/api/alerts/dispatch";

    private readonly RentAppFactory _factory;

    public AlertDispatchEndpointTests(RentAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_Without_Token_Is_Unauthorized()
    {
        using var client = _factory.CreateClient();
        var resp = await client.PostAsync(Url, null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_With_Wrong_Token_Is_Unauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DispatchModel.TokenHeader, "not-the-token");

        var resp = await client.PostAsync(Url, null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_With_Token_Runs_The_Engine_And_Returns_A_Summary()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DispatchModel.TokenHeader, RentAppFactory.TestDispatchToken);

        var resp = await client.PostAsync(Url, null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        payload.TryGetProperty("considered", out _).Should().BeTrue();
        payload.TryGetProperty("sent", out _).Should().BeTrue();
        payload.TryGetProperty("failed", out _).Should().BeTrue();
        payload.TryGetProperty("elapsedMs", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_Is_Not_Allowed()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await client.GetAsync(Url);

        // Must be 405 from the handler, not a 302 into the culture-prefixed path — the
        // locale redirect middleware has to leave /api/ alone.
        resp.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Without_A_Configured_Token_The_Endpoint_Denies_It_Exists()
    {
        // Fail closed. A deployment that forgot the secret must be inert, not an open
        // trigger anyone can hammer.
        using var unconfigured = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Alerts:DispatchToken"] = string.Empty
                })));

        using var client = unconfigured.CreateClient();
        client.DefaultRequestHeaders.Add(DispatchModel.TokenHeader, RentAppFactory.TestDispatchToken);

        var resp = await client.PostAsync(Url, null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Concurrency_Gate_Is_Released_Between_Runs()
    {
        // The overlap guard is a static semaphore. If it were ever taken without a matching
        // release, the endpoint would wedge at 409 forever and the digest would silently
        // stop. Two sequential calls must both succeed.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DispatchModel.TokenHeader, RentAppFactory.TestDispatchToken);

        (await client.PostAsync(Url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(Url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Endpoint_Does_Not_Require_An_Antiforgery_Token()
    {
        // Razor Pages validate antiforgery on POST by default, and the GitHub Actions cron
        // sends a bare curl with no browser session. This is the regression guard for that:
        // the shared secret is the only credential the caller needs.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DispatchModel.TokenHeader, RentAppFactory.TestDispatchToken);

        var resp = await client.PostAsync(Url, content: null);

        resp.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
