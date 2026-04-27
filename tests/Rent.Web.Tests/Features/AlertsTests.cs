using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AlertsTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AlertsTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Alerts_Anonymous_Redirects_To_Login()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/renter/alerts");
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Alerts_Create_RequiresCity()
    {
        using var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "alert-no-city");
        var resp = await client.PostAsync("/renter/alerts?handler=Create",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.City", ""),
                new KeyValuePair<string, string>("Input.Frequency", AlertFrequency.Daily.ToString())
            }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("City is required");
    }

    [Fact]
    public async Task Alerts_Create_PersistsAndShowsInList()
    {
        var email = $"alert-create+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        var create = await client.PostAsync("/renter/alerts?handler=Create",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.City", "Toronto"),
                new KeyValuePair<string, string>("Input.PriceMax", "2500"),
                new KeyValuePair<string, string>("Input.BedroomsMin", "2"),
                new KeyValuePair<string, string>("Input.PetsAllowed", "true"),
                new KeyValuePair<string, string>("Input.Frequency", AlertFrequency.Daily.ToString())
            }));
        create.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Alerts.AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .ToListAsync();
            saved.Should().HaveCount(1);
            var a = saved.Single();
            a.City.Should().Be("Toronto");
            a.PriceMax.Should().Be(2500m);
            a.BedroomsMin.Should().Be(2);
            a.PetsAllowed.Should().BeTrue();
            a.Frequency.Should().Be(AlertFrequency.Daily);
            a.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Alerts_Toggle_FlipsIsActive()
    {
        var email = $"alert-toggle+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);

        Guid alertId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = new Alert
            {
                UserId = user.Id,
                City = "Vancouver",
                Frequency = AlertFrequency.Weekly,
                IsActive = true
            };
            db.Alerts.Add(a);
            await db.SaveChangesAsync();
            alertId = a.Id;
        }

        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);
        var resp = await client.PostAsync("/renter/alerts?handler=Toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", alertId.ToString())
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = await db.Alerts.AsNoTracking().FirstAsync(x => x.Id == alertId);
            a.IsActive.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Alerts_Delete_RemovesRow()
    {
        var email = $"alert-delete+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);

        Guid alertId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = new Alert
            {
                UserId = user.Id,
                City = "Calgary",
                Frequency = AlertFrequency.Instant
            };
            db.Alerts.Add(a);
            await db.SaveChangesAsync();
            alertId = a.Id;
        }

        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);
        var resp = await client.PostAsync("/renter/alerts?handler=Delete",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", alertId.ToString())
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exists = await db.Alerts.AnyAsync(x => x.Id == alertId);
            exists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Alerts_OtherUsersAlerts_AreNotVisible()
    {
        // User A creates an alert. User B logs in and lists.
        var aliceEmail = $"alert-alice+{Guid.NewGuid():N}@test.local";
        var alice = await TestAuth.CreateUserAsync(_factory, aliceEmail, Roles.Renter);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Alerts.Add(new Alert { UserId = alice.Id, City = "Ottawa", Frequency = AlertFrequency.Daily });
            await db.SaveChangesAsync();
        }

        using var bob = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "alert-bob");
        bob.DefaultRequestHeaders.Add("Accept", "text/html");

        var resp = await bob.GetAsync("/renter/alerts");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("No alerts set up");
        body.Should().NotContain("Ottawa");
    }
}
