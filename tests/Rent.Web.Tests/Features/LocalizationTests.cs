using System.Globalization;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Rent.Web;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class LocalizationTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public LocalizationTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Localizer_returns_english_by_default()
    {
        using var scope = _factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResource>>();

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            localizer["common.search"].Value.Should().Be("Search");
            localizer["common.signIn"].Value.Should().Be("Sign In");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Localizer_returns_french_when_culture_is_fr()
    {
        using var scope = _factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResource>>();

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr");
            localizer["common.search"].Value.Should().Be("Rechercher");
            localizer["common.signIn"].Value.Should().Be("Connexion");
            localizer["common.landlords"].Value.Should().Be("Propriétaires");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Middleware_redirects_root_to_en_by_default()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/en");
    }

    [Fact]
    public async Task Middleware_redirects_to_fr_when_accept_language_is_french()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fr-CA,fr;q=0.9,en;q=0.8");

        var response = await client.GetAsync("/toronto");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/fr/toronto");
    }

    [Fact]
    public async Task Middleware_redirects_to_locale_from_cookie()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c=fr|uic=fr");

        var response = await client.GetAsync("/landlords?ref=footer");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/fr/landlords?ref=footer");
    }

    [Fact]
    public async Task Middleware_does_not_redirect_api_paths()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var healthResponse = await client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await client.GetAsync("/api/maps/atlantis");
        apiResponse.StatusCode.Should().NotBe(HttpStatusCode.Found);
    }

    [Fact]
    public async Task Middleware_does_not_redirect_when_locale_already_in_path()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/en");

        response.StatusCode.Should().NotBe(HttpStatusCode.Found);
    }

    [Theory]
    [InlineData("/en/toronto", "fr", "/fr/toronto")]
    [InlineData("/fr/toronto", "en", "/en/toronto")]
    [InlineData("/en/toronto?Sort=PriceAsc", "fr", "/fr/toronto?Sort=PriceAsc")]
    [InlineData("/", "fr", "/fr")]
    [InlineData("/toronto", "fr", "/fr/toronto")]
    [InlineData("//evil.com/x", "fr", "/fr")]
    [InlineData("https://evil.com/x", "fr", "/fr")]
    [InlineData(null, "fr", "/fr")]
    public void NormalizeReturnUrl_swaps_locale_or_falls_back_to_home(string? input, string target, string expected)
    {
        var result = Rent.Web.Features.Localization.Pages.SetLanguageModel
            .NormalizeReturnUrl(input, target);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task SetLanguage_post_sets_cookie_and_redirects()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("culture", "fr"),
            new KeyValuePair<string, string>("returnUrl", "/en/toronto")
        });

        var response = await client.PostAsync("/set-language", form);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/fr/toronto");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        string.Join(";", cookies!).Should().Contain(".AspNetCore.Culture");
    }

    [Fact]
    public void AiSystemPrompt_appends_french_instruction_when_locale_is_fr()
    {
        var prompt = Rent.Web.Features.AiChat.Services.AiSystemPrompt.Build(
            new Rent.Web.Features.AiChat.ChatContext(null, null, null, "fr"));
        prompt.Should().Contain("Always respond in French.");
    }

    [Fact]
    public void AiSystemPrompt_omits_french_instruction_when_locale_is_en()
    {
        var prompt = Rent.Web.Features.AiChat.Services.AiSystemPrompt.Build(
            new Rent.Web.Features.AiChat.ChatContext(null, null, null, "en"));
        prompt.Should().NotContain("Always respond in French.");
    }

    [Fact]
    public async Task Anonymous_protected_page_redirects_to_localized_login()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/fr/renter");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/fr/login");
    }

    [Fact]
    public async Task Welcome_email_uses_french_when_locale_fr()
    {
        var sender = new Rent.Web.Tests.Fixtures.FakeEmailSender();
        await sender.SendWelcomeAsync(new Rent.Web.Features.Email.WelcomeEmail(
            "user@example.com", "Jean", "Renter", "https://example.com/fr/renter", "fr"));

        var (subject, html) = Rent.Web.Features.Email.EmailTemplates.Welcome(new Rent.Web.Features.Email.WelcomeEmail(
            "user@example.com", "Jean", "Renter", "https://example.com/fr/renter", "fr"));

        subject.Should().Be("Bienvenue sur Rent.ca");
        html.Should().Contain("Bienvenue sur Rent.ca.");
        html.Should().Contain("html lang=\"fr\"");
    }
}
