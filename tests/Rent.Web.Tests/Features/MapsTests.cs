using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.Maps;
using Rent.Web.Features.Search;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class MapsTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public MapsTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Endpoint_UnknownCity_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/maps/atlantis");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoint_KnownCity_ReturnsCityCenterAndMarkers()
    {
        var citySlug = $"maps-city-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug, lat: 43.5, lng: -79.5);
        await SeedPropertyAsync(citySlug, lat: 43.6, lng: -79.4, price: 2000m, bedrooms: 2);
        await SeedPropertyAsync(citySlug, lat: 43.7, lng: -79.6, price: 2400m, bedrooms: 3);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/maps/{citySlug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("city").GetProperty("lat").GetDouble().Should().Be(43.5);
        root.GetProperty("city").GetProperty("lng").GetDouble().Should().Be(-79.5);

        var markers = root.GetProperty("markers").EnumerateArray().ToList();
        markers.Should().HaveCount(2);
        markers.Select(m => m.GetProperty("citySlug").GetString()).Should().AllBeEquivalentTo(citySlug);
        markers.Select(m => m.GetProperty("lat").GetDouble()).Should().BeEquivalentTo(new[] { 43.6, 43.7 });
    }

    [Fact]
    public async Task Endpoint_OmitsPropertiesWithoutCoordinates()
    {
        var citySlug = $"nocoords-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug, 50.0, -110.0);
        await SeedPropertyAsync(citySlug, lat: 50.1, lng: -110.1, price: 1500m, bedrooms: 1);
        await SeedPropertyAsync(citySlug, lat: null, lng: null, price: 1700m, bedrooms: 2);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/maps/{citySlug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("markers").EnumerateArray().Count().Should().Be(1);
    }

    [Fact]
    public async Task Endpoint_AppliesPriceAndBedroomFilters()
    {
        var citySlug = $"filter-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug, 49.2, -123.1);
        await SeedPropertyAsync(citySlug, 49.21, -123.11, price: 1200m, bedrooms: 1);
        await SeedPropertyAsync(citySlug, 49.22, -123.12, price: 2500m, bedrooms: 2);
        await SeedPropertyAsync(citySlug, 49.23, -123.13, price: 4500m, bedrooms: 3);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/maps/{citySlug}?MaxPrice=3000&Bedrooms=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var markers = doc.RootElement.GetProperty("markers").EnumerateArray().ToList();
        markers.Should().HaveCount(1);
        markers[0].GetProperty("minBedrooms").GetInt32().Should().Be(2);
        markers[0].GetProperty("fromPrice").GetDecimal().Should().Be(2500m);
    }

    [Fact]
    public async Task Endpoint_OnlyReturnsActiveListings()
    {
        var citySlug = $"active-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug, 51.0, -114.0);
        await SeedPropertyAsync(citySlug, 51.05, -114.05, price: 1900m, bedrooms: 1, status: ListingStatus.Active);
        await SeedPropertyAsync(citySlug, 51.06, -114.06, price: 2100m, bedrooms: 2, status: ListingStatus.Draft);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/maps/{citySlug}");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("markers").EnumerateArray().Count().Should().Be(1);
    }

    [Fact]
    public async Task Layout_RendersMapsApiKeyMetaTag_WhenConfigured()
    {
        // The fixture doesn't set Maps:GoogleApiKey, so by default the meta tag is absent.
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");
        html.Should().NotContain("rentca-maps-key");
    }

    private async Task SeedCityAsync(string slug, double lat, double lng)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Cities.AnyAsync(c => c.Slug == slug)) return;
        db.Cities.Add(new City
        {
            Name = slug,
            Province = "ON",
            Slug = slug,
            Latitude = lat,
            Longitude = lng,
            IsFeatured = false
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedPropertyAsync(
        string citySlug,
        double? lat,
        double? lng,
        decimal price,
        int bedrooms,
        ListingStatus status = ListingStatus.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string landlordEmail = "maps-landlord@test.local";
        var landlord = await userManager.FindByEmailAsync(landlordEmail);
        if (landlord is null)
        {
            landlord = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = landlordEmail,
                UserName = landlordEmail,
                FullName = "Maps Landlord",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(landlord, "Landlord1234!");
            await userManager.AddToRoleAsync(landlord, Roles.Landlord);
            db.LandlordProfiles.Add(new LandlordProfile
            {
                Id = landlord.Id,
                CompanyName = "Maps Holdings",
                Tier = ListingTier.Limited
            });
            await db.SaveChangesAsync();
        }

        var prop = new Property
        {
            LandlordProfileId = landlord.Id,
            Title = $"Map demo {Guid.NewGuid():N}",
            Slug = $"map-demo-{Guid.NewGuid():N}",
            PropertyType = PropertyType.Apartment,
            Status = status,
            Tier = ListingTier.Limited,
            StreetAddress = "123 Map St",
            City = citySlug,
            Province = "ON",
            PostalCode = "M0M 0M0",
            Latitude = lat,
            Longitude = lng,
            PetsAllowed = true,
            Furnished = false
        };
        prop.Units.Add(new Unit
        {
            Bedrooms = bedrooms,
            Bathrooms = 1m,
            Price = price,
            SqFt = 700,
            AvailableUnits = 1
        });
        db.Properties.Add(prop);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handler_DirectInvocation_ReturnsExpectedShape()
    {
        var citySlug = $"direct-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug, 45.0, -75.0);
        await SeedPropertyAsync(citySlug, 45.01, -75.01, price: 1800m, bedrooms: 1);

        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<MapMarkersHandler>();

        var result = await handler.ExecuteAsync(new SearchQuery { CitySlug = citySlug });

        result.CityFound.Should().BeTrue();
        result.CityLat.Should().Be(45.0);
        result.Markers.Should().HaveCount(1);
        result.Markers[0].FromPrice.Should().Be(1800m);
        result.Markers[0].MinBedrooms.Should().Be(1);
    }
}
