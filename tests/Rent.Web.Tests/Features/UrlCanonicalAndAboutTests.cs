using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class UrlCanonicalAndAboutTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public UrlCanonicalAndAboutTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task About_Returns200_WithExpectedCopy()
    {
        using var client = _factory.CreateClient();

        var enResponse = await client.GetAsync("/en/about");
        enResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var enBody = await enResponse.Content.ReadAsStringAsync();
        enBody.Should().Contain("Our Mission");
        enBody.Should().Contain("AI-Powered Search");

        var frResponse = await client.GetAsync("/fr/about");
        frResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var frBody = WebUtility.HtmlDecode(await frResponse.Content.ReadAsStringAsync());
        frBody.Should().Contain("Notre mission");
        frBody.Should().Contain("Recherche assistée par IA");
    }

    [Fact]
    public async Task UppercasePath_Returns301ToLowercase()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/en/Toronto");

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.Should().Be("/en/toronto");
    }

    [Fact]
    public async Task CanonicalTag_Present_AndAlwaysLowercase()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/en/about");

        html.Should().Contain("rel=\"canonical\"");
        html.Should().Contain("/en/about");
        html.Should().NotContain("/en/About");
    }
}
