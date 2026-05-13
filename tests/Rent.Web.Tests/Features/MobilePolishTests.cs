using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class MobilePolishTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public MobilePolishTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DarkToggle_RendersInBothDesktopBarAndMobileDrawer()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/en");

        // Desktop bar (hidden sm:inline-flex) + mobile drawer copy = at least 2 toggles.
        // Both share the same data-theme-toggle handler, so state stays in sync.
        var matches = Regex.Matches(html, "data-theme-toggle");
        matches.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task FrLandlords_UsesIANotAI()
    {
        using var client = _factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/fr/landlords"));

        // Must use the French acronym IA (intelligence artificielle), never AI.
        html.Should().NotContain("par AI");
        html.Should().NotContain("assistant AI");
        html.Should().NotContain("boost AI");
        html.Should().Contain("propulsé par IA");
    }
}
