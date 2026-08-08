using System.Net;
using FluentAssertions;
using Rent.Web.Features.Email;

namespace Rent.Web.Tests.Features;

public class AlertDigestTemplateTests
{
    private static AlertDigestEmail Sample(
        string locale = "en",
        string? alertName = "Downtown 2BR",
        int totalMatches = 2,
        int itemCount = 2) =>
        new(
            ToEmail: "renter@test.local",
            ToName: "Ana",
            AlertName: alertName,
            AlertSummary: "Toronto",
            Items: Enumerable.Range(1, itemCount).Select(i => new AlertDigestItem(
                Title: $"Listing {i}",
                Url: $"https://example.test/en/listing/listing-{i}",
                Location: "King West, Toronto",
                MinPrice: 1800m,
                MaxPrice: i == 1 ? 2400m : null,
                MinBedrooms: 2,
                MaxBedrooms: null,
                MinBathrooms: 1.5m,
                ImageUrl: "https://example.test/img.jpg")).ToList(),
            TotalMatches: totalMatches,
            SearchUrl: "https://example.test/en/toronto",
            ManageAlertsUrl: "https://example.test/en/renter/alerts",
            Locale: locale);

    [Fact]
    public void English_Subject_Uses_Count_And_Alert_Name()
    {
        var (subject, _) = EmailTemplates.AlertDigest(Sample());
        subject.Should().Be("2 new listings for \"Downtown 2BR\"");
    }

    [Fact]
    public void French_Subject_Is_Localized()
    {
        var (subject, html) = EmailTemplates.AlertDigest(Sample(locale: "fr"));

        subject.Should().Be("2 nouvelles annonces pour « Downtown 2BR »");
        html.Should().Contain("lang=\"fr\"");

        // Button labels go through HtmlEncode, so accented text lands as entities
        // ("G&#233;rer"). Decode before asserting on the copy a reader actually sees.
        var rendered = WebUtility.HtmlDecode(html);
        rendered.Should().Contain("Bonjour Ana");
        rendered.Should().Contain("Gérer mes alertes");
        rendered.Should().Contain("Voir toutes les annonces");
        // The digest must not leak English chrome into a French email.
        rendered.Should().NotContain("Manage alerts");
        rendered.Should().NotContain("View all listings");
    }

    [Fact]
    public void French_Prices_Use_Canadian_French_Currency_Format()
    {
        var (_, html) = EmailTemplates.AlertDigest(Sample(locale: "fr"));
        var rendered = WebUtility.HtmlDecode(html);

        // fr-CA puts the symbol after the amount with a non-breaking space: "1 800 $".
        rendered.Should().Contain("800 $");
        rendered.Should().NotContain("$1,800");
        // Decimal comma, not point, in the bathroom count.
        rendered.Should().Contain("1,5 sdb.");
    }

    [Fact]
    public void Singular_Count_Uses_Singular_Noun_In_Both_Locales()
    {
        EmailTemplates.AlertDigest(Sample(totalMatches: 1, itemCount: 1)).Subject
            .Should().Be("1 new listing for \"Downtown 2BR\"");

        EmailTemplates.AlertDigest(Sample(locale: "fr", totalMatches: 1, itemCount: 1)).Subject
            .Should().Be("1 nouvelle annonce pour « Downtown 2BR »");
    }

    [Fact]
    public void Falls_Back_To_Summary_When_Alert_Has_No_Name()
    {
        var (subject, _) = EmailTemplates.AlertDigest(Sample(alertName: null));
        subject.Should().Be("2 new listings for \"Toronto\"");
    }

    [Fact]
    public void Shows_Remainder_When_Items_Were_Capped()
    {
        // 7 matched, only 2 fit in the email.
        var (_, html) = EmailTemplates.AlertDigest(Sample(totalMatches: 7, itemCount: 2));
        html.Should().Contain("+ 5 more matches.");
    }

    [Fact]
    public void Omits_Remainder_Line_When_Nothing_Was_Capped()
    {
        var (_, html) = EmailTemplates.AlertDigest(Sample(totalMatches: 2, itemCount: 2));
        html.Should().NotContain("more match");
    }

    [Fact]
    public void Renders_Listing_Links_And_Manage_Alerts_Link()
    {
        var (_, html) = EmailTemplates.AlertDigest(Sample());

        html.Should().Contain("https://example.test/en/listing/listing-1");
        html.Should().Contain("https://example.test/en/renter/alerts");
        html.Should().Contain("https://example.test/en/toronto");
    }

    [Fact]
    public void Formats_Price_Range_And_Single_Price()
    {
        var (_, html) = EmailTemplates.AlertDigest(Sample());

        // Item 1 has a max price -> range; item 2 does not -> single value.
        html.Should().Contain("$1,800");
        html.Should().Contain("$2,400");
    }

    [Fact]
    public void Escapes_Html_In_User_Controlled_Fields()
    {
        var hostile = Sample(alertName: "<script>alert(1)</script>") with
        {
            ToName = "<b>Ana</b>"
        };

        var (subject, html) = EmailTemplates.AlertDigest(hostile);

        // The subject is a plain-text header, but the body must never carry raw markup
        // that a renter typed into the alert name field.
        subject.Should().Contain("<script>");
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<b>Ana</b>");
    }
}
