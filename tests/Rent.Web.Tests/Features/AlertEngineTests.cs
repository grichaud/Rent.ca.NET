using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.Alerts.Engine;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

/// <summary>
/// Cadence rules. Pure functions, so these need no fixture at all.
/// </summary>
public class AlertScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private static Alert Make(AlertFrequency freq, DateTimeOffset? lastSent, bool active = true) =>
        new()
        {
            Frequency = freq,
            LastSentAt = lastSent,
            IsActive = active,
            CreatedAt = Now.AddDays(-30)
        };

    [Fact]
    public void NeverSent_IsDue_Regardless_Of_Frequency()
    {
        AlertSchedule.IsDue(Make(AlertFrequency.Daily, null), Now).Should().BeTrue();
        AlertSchedule.IsDue(Make(AlertFrequency.Weekly, null), Now).Should().BeTrue();
        AlertSchedule.IsDue(Make(AlertFrequency.Instant, null), Now).Should().BeTrue();
    }

    [Fact]
    public void PausedAlert_IsNeverDue()
    {
        AlertSchedule.IsDue(Make(AlertFrequency.Instant, null, active: false), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Daily_NotDue_TwoHours_After_Send()
    {
        AlertSchedule.IsDue(Make(AlertFrequency.Daily, Now.AddHours(-2)), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Daily_IsDue_At_TwentyThreeHours_Not_TwentyFour()
    {
        // The 23h slack is what stops hourly-cron jitter from drifting a daily digest
        // into every-other-day. 22h59m must not fire; 23h01m must.
        AlertSchedule.IsDue(Make(AlertFrequency.Daily, Now.AddHours(-22).AddMinutes(-59)), Now)
            .Should().BeFalse();
        AlertSchedule.IsDue(Make(AlertFrequency.Daily, Now.AddHours(-23).AddMinutes(-1)), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Weekly_NotDue_After_Three_Days_But_Due_After_Seven()
    {
        AlertSchedule.IsDue(Make(AlertFrequency.Weekly, Now.AddDays(-3)), Now)
            .Should().BeFalse();
        AlertSchedule.IsDue(Make(AlertFrequency.Weekly, Now.AddDays(-7)), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Instant_IsDue_Every_Run()
    {
        AlertSchedule.IsDue(Make(AlertFrequency.Instant, Now.AddMinutes(-5)), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void WindowStart_Falls_Back_To_CreatedAt_When_Never_Sent()
    {
        var alert = Make(AlertFrequency.Daily, null);
        AlertSchedule.WindowStart(alert).Should().Be(alert.CreatedAt);

        var sent = Make(AlertFrequency.Daily, Now.AddDays(-1));
        AlertSchedule.WindowStart(sent).Should().Be(Now.AddDays(-1));
    }
}

/// <summary>
/// Matching rules against a real (SQLite) database.
/// </summary>
public class AlertMatcherTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AlertMatcherTests(RentAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Returns_Only_Properties_Created_After_The_Window_Start()
    {
        var city = UniqueCity();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-5);

        await SeedPropertyAsync(city, createdAt: cutoff.AddDays(-1), price: 1500); // too old
        var fresh = await SeedPropertyAsync(city, createdAt: cutoff.AddDays(1), price: 1600);

        var matches = await MatchAsync(new Alert { City = city }, cutoff);

        matches.Should().ContainSingle();
        matches[0].PropertyId.Should().Be(fresh);
    }

    [Fact]
    public async Task Excludes_Listings_That_Are_Not_Active()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        await SeedPropertyAsync(city, status: ListingStatus.Draft);
        var active = await SeedPropertyAsync(city, status: ListingStatus.Active);

        var matches = await MatchAsync(new Alert { City = city }, since);

        matches.Should().ContainSingle();
        matches[0].PropertyId.Should().Be(active);
    }

    [Fact]
    public async Task Honours_PriceMax_Against_Units()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        await SeedPropertyAsync(city, price: 4000);
        var cheap = await SeedPropertyAsync(city, price: 1800);

        var matches = await MatchAsync(new Alert { City = city, PriceMax = 2000m }, since);

        matches.Should().ContainSingle();
        matches[0].PropertyId.Should().Be(cheap);
    }

    [Fact]
    public async Task Honours_BedroomsMin_And_BathroomsMin()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        await SeedPropertyAsync(city, bedrooms: 1, bathrooms: 1m);
        var big = await SeedPropertyAsync(city, bedrooms: 3, bathrooms: 2m);

        var byBed = await MatchAsync(new Alert { City = city, BedroomsMin = 2 }, since);
        byBed.Should().ContainSingle();
        byBed[0].PropertyId.Should().Be(big);

        // BathroomsMin was a dead column before the engine existed — this is the first
        // code path that reads it.
        var byBath = await MatchAsync(new Alert { City = city, BathroomsMin = 1.5m }, since);
        byBath.Should().ContainSingle();
        byBath[0].PropertyId.Should().Be(big);
    }

    [Fact]
    public async Task PetsAllowed_Is_Tri_State()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        var pets = await SeedPropertyAsync(city, petsAllowed: true);
        var noPets = await SeedPropertyAsync(city, petsAllowed: false);

        (await MatchAsync(new Alert { City = city, PetsAllowed = true }, since))
            .Select(m => m.PropertyId).Should().BeEquivalentTo(new[] { pets });

        (await MatchAsync(new Alert { City = city, PetsAllowed = false }, since))
            .Select(m => m.PropertyId).Should().BeEquivalentTo(new[] { noPets });

        // null = "any" — both come back.
        (await MatchAsync(new Alert { City = city, PetsAllowed = null }, since))
            .Should().HaveCount(2);
    }

    [Fact]
    public async Task Honours_PropertyType_And_City()
    {
        var city = UniqueCity();
        var otherCity = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        var condo = await SeedPropertyAsync(city, type: PropertyType.Condo);
        await SeedPropertyAsync(city, type: PropertyType.House);
        await SeedPropertyAsync(otherCity, type: PropertyType.Condo);

        var matches = await MatchAsync(
            new Alert { City = city, PropertyType = PropertyType.Condo }, since);

        matches.Should().ContainSingle();
        matches[0].PropertyId.Should().Be(condo);
    }

    [Fact]
    public async Task Caps_Results_And_Returns_Newest_First()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-10);

        var oldest = await SeedPropertyAsync(city, createdAt: since.AddDays(1));
        var middle = await SeedPropertyAsync(city, createdAt: since.AddDays(2));
        var newest = await SeedPropertyAsync(city, createdAt: since.AddDays(3));

        var all = await MatchAsync(new Alert { City = city }, since);
        all.Select(m => m.PropertyId).Should().ContainInOrder(newest, middle, oldest);

        var capped = await MatchAsync(new Alert { City = city }, since, max: 2);
        capped.Should().HaveCount(2);
        capped.Select(m => m.PropertyId).Should().ContainInOrder(newest, middle);
    }

    [Fact]
    public async Task Projects_Price_Range_Across_Units()
    {
        var city = UniqueCity();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        await SeedPropertyAsync(city, price: 1500, extraUnitPrice: 2400);

        var matches = await MatchAsync(new Alert { City = city }, since);

        matches.Should().ContainSingle();
        matches[0].MinPrice.Should().Be(1500m);
        matches[0].MaxPrice.Should().Be(2400m);
    }

    // ---- helpers -------------------------------------------------------------

    private async Task<IReadOnlyList<AlertMatch>> MatchAsync(
        Alert alert, DateTimeOffset since, int max = 10)
    {
        using var scope = _factory.Services.CreateScope();
        var matcher = scope.ServiceProvider.GetRequiredService<AlertMatcher>();
        return await matcher.FindNewMatchesAsync(alert, since, max);
    }

    /// <summary>Each test gets its own city so the shared fixture database stays isolated.</summary>
    private static string UniqueCity() => $"Testville-{Guid.NewGuid():N}"[..24];

    private async Task<Guid> SeedPropertyAsync(
        string city,
        DateTimeOffset? createdAt = null,
        decimal price = 2000,
        int bedrooms = 2,
        decimal bathrooms = 1m,
        bool petsAllowed = true,
        PropertyType type = PropertyType.Apartment,
        ListingStatus status = ListingStatus.Active,
        decimal? extraUnitPrice = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = $"engine-landlord+{Guid.NewGuid():N}@test.local",
            UserName = $"engine-landlord+{Guid.NewGuid():N}@test.local",
            FullName = "Engine Landlord",
            EmailConfirmed = true
        };
        db.Users.Add(landlord);
        db.LandlordProfiles.Add(new LandlordProfile { Id = landlord.Id, Tier = ListingTier.Limited });

        var propId = Guid.NewGuid();
        var property = new Property
        {
            Id = propId,
            LandlordProfileId = landlord.Id,
            Title = $"Engine Test {propId:N}"[..24],
            Slug = $"engine-test-{propId:N}",
            City = city,
            Province = "ON",
            PostalCode = "M5V 1A1",
            StreetAddress = "1 Engine St",
            PropertyType = type,
            Status = status,
            Tier = ListingTier.Limited,
            PetsAllowed = petsAllowed,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        db.Properties.Add(property);

        db.Units.Add(new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = propId,
            Price = price,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms
        });

        if (extraUnitPrice is decimal extra)
        {
            db.Units.Add(new Unit
            {
                Id = Guid.NewGuid(),
                PropertyId = propId,
                Price = extra,
                Bedrooms = bedrooms,
                Bathrooms = bathrooms
            });
        }

        await db.SaveChangesAsync();
        return propId;
    }
}
