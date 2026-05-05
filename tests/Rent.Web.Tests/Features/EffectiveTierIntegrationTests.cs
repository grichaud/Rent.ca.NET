using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.Search;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class EffectiveTierIntegrationTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public EffectiveTierIntegrationTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchHandler_PropertyWithExpiredTier_ResolvesToLimited()
    {
        var citySlug = $"exptier-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug);

        var expiredId = await SeedPropertyAsync(
            citySlug,
            tier: ListingTier.Featured,
            tierExpiresAt: DateTimeOffset.UtcNow.AddHours(-1));

        var validId = await SeedPropertyAsync(
            citySlug,
            tier: ListingTier.Featured,
            tierExpiresAt: DateTimeOffset.UtcNow.AddDays(7));

        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SearchHandler>();

        var result = await handler.ExecuteAsync(new SearchQuery { CitySlug = citySlug });

        result.Properties.Single(p => p.Id == expiredId).EffectiveTier
            .Should().Be(ListingTier.Limited);
        result.Properties.Single(p => p.Id == validId).EffectiveTier
            .Should().Be(ListingTier.Featured);
    }

    [Fact]
    public async Task SearchHandler_RanksValidFeaturedAboveExpiredFeatured()
    {
        var citySlug = $"ranking-{Guid.NewGuid():N}".Substring(0, 16);
        await SeedCityAsync(citySlug);

        // Create expired Featured FIRST (with earlier CreatedAt) and valid Featured second.
        // Without EffectiveTier sort, the expired one would still be sorted as Featured.
        var expiredId = await SeedPropertyAsync(
            citySlug,
            tier: ListingTier.Featured,
            tierExpiresAt: DateTimeOffset.UtcNow.AddHours(-1));

        await Task.Delay(50);

        var validId = await SeedPropertyAsync(
            citySlug,
            tier: ListingTier.Featured,
            tierExpiresAt: null);

        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SearchHandler>();

        var result = await handler.ExecuteAsync(new SearchQuery
        {
            CitySlug = citySlug,
            Sort = SearchSort.Newest
        });

        var ordered = result.Properties.Select(p => p.Id).ToList();
        ordered.IndexOf(validId).Should().BeLessThan(ordered.IndexOf(expiredId),
            because: "live Featured should rank above expired Featured");
    }

    private async Task SeedCityAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Cities.AnyAsync(c => c.Slug == slug)) return;
        db.Cities.Add(new City
        {
            Name = slug,
            Province = "ON",
            Slug = slug,
            IsFeatured = false
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedPropertyAsync(
        string citySlug,
        ListingTier tier,
        DateTimeOffset? tierExpiresAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var landlordEmail = $"effective-{Guid.NewGuid():N}@test.local";
        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = landlordEmail,
            UserName = landlordEmail,
            FullName = "Effective Tier Landlord",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(landlord, "Landlord1234!");
        await userManager.AddToRoleAsync(landlord, Roles.Landlord);
        db.LandlordProfiles.Add(new LandlordProfile
        {
            Id = landlord.Id,
            CompanyName = "Effective Holdings",
            Tier = ListingTier.Limited
        });
        await db.SaveChangesAsync();

        var prop = new Property
        {
            LandlordProfileId = landlord.Id,
            Title = $"Effective demo {Guid.NewGuid():N}",
            Slug = $"effective-demo-{Guid.NewGuid():N}",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = tier,
            TierExpiresAt = tierExpiresAt,
            StreetAddress = "123 Effective St",
            City = citySlug,
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
