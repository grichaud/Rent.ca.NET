using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AdminAuthTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AdminAuthTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_GetAdmin_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/en/admin");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("/en/login");
    }

    [Fact]
    public async Task Renter_GetAdmin_RedirectsAway()
    {
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "admin-renter");

        var response = await client.GetAsync("/en/admin");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("/en/login");
    }

    [Fact]
    public async Task Landlord_GetAdmin_RedirectsAway()
    {
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Landlord, "admin-landlord");

        var response = await client.GetAsync("/en/admin");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("/en/login");
    }

    [Fact]
    public async Task Admin_GetAdmin_Returns200()
    {
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "admin-ok");

        var response = await client.GetAsync("/en/admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_GetAdmin_RendersDashboardCards()
    {
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "admin-render");

        var html = await client.GetStringAsync("/en/admin");

        html.Should().Contain("Admin Dashboard");
        html.Should().Contain("/en/admin/properties");
        html.Should().Contain("/en/admin/specials");
        html.Should().Contain("/en/admin/ai");
    }

    [Fact]
    public async Task Login_AdminUser_RedirectsToAdminDashboard()
    {
        var email = $"login-admin+{Guid.NewGuid():N}@test.local";
        await TestAuth.CreateUserAsync(_factory, email, Roles.Admin);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", TestAuth.DefaultPassword),
            new KeyValuePair<string, string>("Input.RememberMe", "false")
        });

        var response = await client.PostAsync("/en/login", form);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().EndWith("/en/admin");
    }
}
