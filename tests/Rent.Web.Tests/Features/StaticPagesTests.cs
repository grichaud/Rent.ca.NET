using System.Linq;
using System.Net;
using System.Web;
using FluentAssertions;
using Rent.Web.Features.Home;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

/// <summary>
/// Fija el contenido de las páginas estáticas. La matriz de tiers se assertea contra el
/// PageModel directamente: un smoke de 200 no ve un booleano invertido.
/// </summary>
public class StaticPagesTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public StaticPagesTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void PricingTiers_MatchAdvertisedMatrix()
    {
        var model = new LandlordsLandingModel();

        var limited = model.Tiers.Single(t => t.NameKey == "Landlords_TierLimited");
        limited.Features.Count(f => f.Included).Should().Be(4);
        limited.Features.Where(f => !f.Included).Select(f => f.LabelKey)
            .Should().BeEquivalentTo("Landlords_TierLimitedF5", "Landlords_TierLimitedF6",
                                     "Landlords_TierLimitedF7", "Landlords_TierLimitedF8");

        var promoted = model.Tiers.Single(t => t.NameKey == "Landlords_TierPromoted");
        promoted.Features.Count(f => f.Included).Should().Be(5);
        promoted.Features.Where(f => !f.Included).Select(f => f.LabelKey)
            .Should().BeEquivalentTo("Landlords_TierPromotedF6", "Landlords_TierPromotedF7",
                                     "Landlords_TierPromotedF8");

        var featured = model.Tiers.Single(t => t.NameKey == "Landlords_TierFeatured");
        featured.Features.Should().OnlyContain(f => f.Included);
    }

    [Fact]
    public async Task LandlordsLanding_RendersExcludedFeaturesAndPrices()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/en/landlords");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("line-through");
        body.Should().Contain("$35");
        body.Should().Contain("$199");
    }

    [Fact]
    public async Task Privacy_IsHonestAboutDataSharing()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/en/privacy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        body.Should().Contain("portfolio project");
        // La app sí comparte datos con el landlord de la ficha (Inquiries -> Inbox).
        body.Should().NotContain("No personal data is shared with third parties");
    }

    [Fact]
    public async Task Privacy_Fr_IsLocalized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/fr/privacy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        body.Should().Contain("projet portfolio");
        body.Should().NotContain("Aucune donnée personnelle n'est partagée avec des tiers");
    }

    [Fact]
    public async Task Faq_Returns200_InBothCultures()
    {
        using var client = _factory.CreateClient();

        var en = await client.GetAsync("/en/faq");
        en.StatusCode.Should().Be(HttpStatusCode.OK);
        var enBody = HttpUtility.HtmlDecode(await en.Content.ReadAsStringAsync());
        enBody.Should().Contain("Frequently Asked Questions");
        enBody.Should().Contain("How do I contact a landlord?");

        var fr = await client.GetAsync("/fr/faq");
        fr.StatusCode.Should().Be(HttpStatusCode.OK);
        var frBody = HttpUtility.HtmlDecode(await fr.Content.ReadAsStringAsync());
        frBody.Should().Contain("Foire aux questions");
        frBody.Should().Contain("Comment contacter un propriétaire?");
    }
}
