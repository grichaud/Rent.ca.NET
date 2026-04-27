using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class RenterPortalTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public RenterPortalTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_AnonymousIsRedirectedToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/renter");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Dashboard_RequiresRenterRole_LandlordGetsAccessDenied()
    {
        using var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Landlord, "landlord-blocked");
        var response = await client.GetAsync("/renter");

        // ASP.NET Identity default for forbidden returns redirect to /Identity/Account/AccessDenied
        // (Cookie auth maps 403 to 302). The exact location is configurable; what matters is that
        // a logged-in landlord does NOT get a 200 with the dashboard content.
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_AsRenter_ReturnsOkWithStats()
    {
        var email = $"renter-stats+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter, "Stats Tester");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var someProperty = await db.Properties.AsNoTracking().FirstOrDefaultAsync();
            // If there are no seeded properties, we still want to assert the page renders.
            if (someProperty is not null)
            {
                db.Favorites.Add(new Favorite { UserId = user.Id, PropertyId = someProperty.Id });
            }
            db.Alerts.Add(new Alert { UserId = user.Id, City = "Toronto", Frequency = AlertFrequency.Daily, IsActive = true });
            db.Alerts.Add(new Alert { UserId = user.Id, City = "Vancouver", Frequency = AlertFrequency.Weekly, IsActive = false });
            await db.SaveChangesAsync();
        }

        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        var response = await client.GetAsync("/renter");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Saved Properties");
        body.Should().Contain("Active Alerts");
        body.Should().Contain("Inquiries Sent");
        body.Should().Contain("Hi, Stats");
    }

    [Fact]
    public async Task Navbar_ShowsMyPortalLinkForRenter()
    {
        using var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "renter-navbar");
        // Need to follow the post-login redirect to read a normal page
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("My Portal");
    }
}
