using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AuthTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AuthTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsOkPage()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Welcome back");
    }

    [Fact]
    public async Task Signup_ReturnsOkPage()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/signup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Create your account");
    }

    [Fact]
    public async Task SignupAsLandlord_CreatesLandlordProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "harness.landlord@example.com",
            UserName = "harness.landlord@example.com",
            FullName = "Harness Landlord"
        };
        var created = await userManager.CreateAsync(user, "Harness1234!");
        created.Succeeded.Should().BeTrue();

        await userManager.AddToRoleAsync(user, Roles.Landlord);
        var inRole = await userManager.IsInRoleAsync(user, Roles.Landlord);
        inRole.Should().BeTrue();
    }

    [Fact]
    public async Task ProtectedPage_AnonymousRedirectsToLogin()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/landlord");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Login_ShowsContinueWithGoogleButton_WhenProviderConfigured()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Continue with Google");
    }

    [Fact]
    public async Task ExternalLogin_LinkedToUser_PersistsAndRoundTrips()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "external.landlord@example.com",
            UserName = "external.landlord@example.com",
            FullName = "External Landlord",
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(user);
        created.Succeeded.Should().BeTrue();

        await userManager.AddToRoleAsync(user, Roles.Landlord);

        var loginInfo = new UserLoginInfo("Google", "google-sub-abcdef", "Google");
        var addLogin = await userManager.AddLoginAsync(user, loginInfo);
        addLogin.Succeeded.Should().BeTrue();

        var logins = await userManager.GetLoginsAsync(user);
        logins.Should().Contain(l => l.LoginProvider == "Google" && l.ProviderKey == "google-sub-abcdef");

        var roundTrip = await userManager.FindByLoginAsync("Google", "google-sub-abcdef");
        roundTrip.Should().NotBeNull();
        roundTrip!.Email.Should().Be("external.landlord@example.com");
    }
}
