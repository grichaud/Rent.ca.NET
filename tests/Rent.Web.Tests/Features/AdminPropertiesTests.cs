using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AdminPropertiesTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AdminPropertiesTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SetTier_ValidRequest_UpdatesPropertyTier()
    {
        var propId = await SeedPropertyAsync(initialTier: ListingTier.Limited);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "props-set");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("TargetId", propId.ToString()),
            new KeyValuePair<string, string>("Tier", nameof(ListingTier.Featured)),
            new KeyValuePair<string, string>("ExpiresAt", DateTimeOffset.UtcNow.AddDays(30).UtcDateTime.ToString("yyyy-MM-ddTHH:mm"))
        });
        var response = await client.PostAsync("/en/admin/properties?handler=SetTier", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await db.Properties.AsNoTracking().FirstAsync(p => p.Id == propId);
        refreshed.Tier.Should().Be(ListingTier.Featured);
        refreshed.TierExpiresAt.Should().NotBeNull();
        refreshed.TierExpiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task SetTier_PastExpiration_RejectedByValidator()
    {
        var propId = await SeedPropertyAsync(initialTier: ListingTier.Limited);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "props-past");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("TargetId", propId.ToString()),
            new KeyValuePair<string, string>("Tier", nameof(ListingTier.Featured)),
            new KeyValuePair<string, string>("ExpiresAt", DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm"))
        });
        var response = await client.PostAsync("/en/admin/properties?handler=SetTier", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await db.Properties.AsNoTracking().FirstAsync(p => p.Id == propId);
        refreshed.Tier.Should().Be(ListingTier.Limited, because: "validator rejected the future-only constraint");
    }

    [Fact]
    public async Task SetTier_DemoteToLimited_ClearsExpiration()
    {
        var propId = await SeedPropertyAsync(
            initialTier: ListingTier.Featured,
            initialExpiresAt: DateTimeOffset.UtcNow.AddDays(15));
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "props-clear");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("TargetId", propId.ToString()),
            new KeyValuePair<string, string>("Tier", nameof(ListingTier.Limited)),
            new KeyValuePair<string, string>("ExpiresAt", string.Empty)
        });
        var response = await client.PostAsync("/en/admin/properties?handler=SetTier", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await db.Properties.AsNoTracking().FirstAsync(p => p.Id == propId);
        refreshed.Tier.Should().Be(ListingTier.Limited);
        refreshed.TierExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task PropertiesPage_ListIncludesSeededProperty()
    {
        var propId = await SeedPropertyAsync(initialTier: ListingTier.Promoted, titlePrefix: "Listable");
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "props-list");

        var resp = await client.GetAsync("/en/admin/properties");
        var body = await resp.Content.ReadAsStringAsync();
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException($"Status {resp.StatusCode}\n---BODY---\n{body.Substring(0, Math.Min(body.Length, 3000))}");
        }

        body.Should().Contain("Listable");
        body.Should().Contain("PROMOTED");
    }

    private async Task<Guid> SeedPropertyAsync(
        ListingTier initialTier,
        DateTimeOffset? initialExpiresAt = null,
        string titlePrefix = "Demo")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var landlordEmail = $"props-landlord-{Guid.NewGuid():N}@test.local";
        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = landlordEmail,
            UserName = landlordEmail,
            FullName = "Props Landlord",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(landlord, "Landlord1234!");
        await userManager.AddToRoleAsync(landlord, Roles.Landlord);
        db.LandlordProfiles.Add(new LandlordProfile { Id = landlord.Id, CompanyName = "Props Co" });
        await db.SaveChangesAsync();

        var prop = new Property
        {
            LandlordProfileId = landlord.Id,
            Title = $"{titlePrefix} {Guid.NewGuid():N}",
            Slug = $"{titlePrefix.ToLowerInvariant()}-{Guid.NewGuid():N}",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = initialTier,
            TierExpiresAt = initialExpiresAt,
            StreetAddress = "1 Admin Way",
            City = "Toronto",
            Province = "ON",
            PostalCode = "M0M 0M0"
        };
        prop.Units.Add(new Unit
        {
            Bedrooms = 2,
            Bathrooms = 1m,
            Price = 2000m,
            SqFt = 750,
            AvailableUnits = 1
        });
        db.Properties.Add(prop);
        await db.SaveChangesAsync();
        return prop.Id;
    }
}
