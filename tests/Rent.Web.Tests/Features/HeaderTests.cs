using System.Net;
using FluentAssertions;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class HeaderTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public HeaderTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Header_SignedOut_SignInUsesGlassButton()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().MatchRegex("<a href=\"/en/login\"[^>]*glass-button");
    }

    [Fact]
    public async Task Header_ThemeToggleRendersBeforeLanguageSwitcher()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var themeIndex = body.IndexOf("data-theme-toggle", StringComparison.Ordinal);
        var langIndex = body.IndexOf("action=\"/set-language\"", StringComparison.Ordinal);
        themeIndex.Should().BeGreaterThan(-1);
        langIndex.Should().BeGreaterThan(-1);
        themeIndex.Should().BeLessThan(langIndex);
    }

    [Fact]
    public async Task Header_SignedInRenter_ShowsPortalLinkAndLogout()
    {
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "header-renter");
        var response = await client.GetAsync("/en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("href=\"/en/renter\"");
        body.Should().Contain("action=\"/logout\"");
    }
}
