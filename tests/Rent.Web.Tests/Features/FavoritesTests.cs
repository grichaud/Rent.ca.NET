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

public class FavoritesTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public FavoritesTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Toggle_AnonymousIsRedirectedToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.PostAsync("/renter/favorites/toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("propertyId", Guid.NewGuid().ToString())
            }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Toggle_AddsAndRemoves()
    {
        var propertyId = await SeedPropertyAsync();
        var email = $"fav-toggle+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        // First POST: should add the favorite (302 redirect to /renter/favorites by default)
        var add = await client.PostAsync("/renter/favorites/toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("propertyId", propertyId.ToString())
            }));
        add.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await AssertFavoriteCountAsync(user.Id, propertyId, 1);

        // Second POST: should remove it
        var remove = await client.PostAsync("/renter/favorites/toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("propertyId", propertyId.ToString())
            }));
        remove.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await AssertFavoriteCountAsync(user.Id, propertyId, 0);
    }

    [Fact]
    public async Task Toggle_WithJsonAcceptHeader_ReturnsJson()
    {
        var propertyId = await SeedPropertyAsync();
        using var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "fav-json");

        var req = new HttpRequestMessage(HttpMethod.Post, "/renter/favorites/toggle")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("propertyId", propertyId.ToString())
            })
        };
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"favorited\":true");
    }

    [Fact]
    public async Task FavoritesIndex_ShowsOnlyOwnFavorites()
    {
        var propertyId = await SeedPropertyAsync();
        var ownerEmail = $"fav-owner+{Guid.NewGuid():N}@test.local";
        var owner = await TestAuth.CreateUserAsync(_factory, ownerEmail, Roles.Renter, "Owner Fav");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Favorites.Add(new Favorite { UserId = owner.Id, PropertyId = propertyId });
            await db.SaveChangesAsync();
        }

        using var client = await TestAuth.SignInAsync(_factory, ownerEmail, TestAuth.DefaultPassword);
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        var resp = await client.GetAsync("/en/renter/favorites");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("My Favorites");
        body.Should().NotContain("No favorites yet");

        // A different renter sees empty state
        using var other = await TestAuth.CreateAndSignInAsync(_factory, Roles.Renter, "fav-other");
        other.DefaultRequestHeaders.Add("Accept", "text/html");
        var otherResp = await other.GetAsync("/en/renter/favorites");
        otherResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var otherBody = await otherResp.Content.ReadAsStringAsync();
        otherBody.Should().Contain("No favorites yet");
    }

    private async Task<Guid> SeedPropertyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var landlordUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = $"landlord-fav+{Guid.NewGuid():N}@test.local",
            UserName = $"landlord-fav+{Guid.NewGuid():N}@test.local",
            FullName = "Fav Landlord",
            EmailConfirmed = true
        };
        db.Users.Add(landlordUser);
        db.LandlordProfiles.Add(new LandlordProfile { Id = landlordUser.Id, Tier = ListingTier.Limited });

        var propId = Guid.NewGuid();
        db.Properties.Add(new Property
        {
            Id = propId,
            LandlordProfileId = landlordUser.Id,
            Title = "Fav Test Property",
            Slug = $"fav-test-{Guid.NewGuid():N}",
            City = "Toronto",
            Province = "ON",
            PostalCode = "M5V 1A1",
            StreetAddress = "1 Test St",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited
        });
        await db.SaveChangesAsync();
        return propId;
    }

    private async Task AssertFavoriteCountAsync(Guid userId, Guid propertyId, int expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Favorites.CountAsync(f => f.UserId == userId && f.PropertyId == propertyId);
        count.Should().Be(expected);
    }
}
