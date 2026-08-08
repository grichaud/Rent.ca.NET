using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.Alerts.Engine;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

[Collection("AlertEngine")]
public class AlertDigestServiceTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AlertDigestServiceTests(RentAppFactory factory)
    {
        _factory = factory;
        _factory.EmailSender.Reset();
    }

    /// <summary>
    /// The engine processes every active alert in the database, and this class shares one
    /// fixture across its tests — so an alert left live by an earlier test would send a
    /// second digest here and break exact counts. Pausing everything first makes each test
    /// the sole author of what is due. Properties can accumulate harmlessly: every test
    /// uses its own city.
    /// </summary>
    private async Task ResetEngineStateAsync()
    {
        _factory.EmailSender.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var live = await db.Alerts.Where(a => a.IsActive).ToListAsync();
        foreach (var alert in live) alert.IsActive = false;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sends_Digest_And_Stamps_LastSentAt()
    {
        await ResetEngineStateAsync();

        var (userId, email) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city, price: 1800);

        var alertId = await SeedAlertAsync(userId, city, name: "Downtown 2BR");

        var result = await RunAsync();

        result.Sent.Should().Be(1);
        result.Failed.Should().Be(0);

        var digest = _factory.EmailSender.AlertDigests
            .Should().ContainSingle(d => d.ToEmail == email).Which;
        digest.AlertName.Should().Be("Downtown 2BR");
        digest.Items.Should().ContainSingle();
        digest.Items[0].Url.Should().Contain($"/en/{city.Slug}/");
        digest.ManageAlertsUrl.Should().StartWith("https://");

        (await GetAlertAsync(alertId)).LastSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Zero_Matches_Sends_Nothing_And_Leaves_LastSentAt_Untouched()
    {
        await ResetEngineStateAsync();

        var (userId, _) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        // No properties seeded for this city at all.

        var alertId = await SeedAlertAsync(userId, city);

        var result = await RunAsync();

        result.Sent.Should().Be(0);
        result.NoMatches.Should().Be(1);
        _factory.EmailSender.AlertDigests.Should().BeEmpty();

        // Critical: NOT stamping keeps the window open, so a listing published tomorrow
        // is still considered "new" for this alert.
        (await GetAlertAsync(alertId)).LastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task Already_Sent_Properties_Are_Not_Repeated()
    {
        await ResetEngineStateAsync();

        var (userId, _) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);

        var alertId = await SeedAlertAsync(userId, city);

        var first = await RunAsync();
        first.Sent.Should().Be(1);

        // Second run: the alert is Instant, so it is due again, but nothing new was published.
        _factory.EmailSender.Reset();
        var second = await RunAsync();

        second.Sent.Should().Be(0);
        _factory.EmailSender.AlertDigests.Should().BeEmpty();

        // A newly published listing does come through, and only that one.
        await SeedPropertyAsync(city, title: "Brand New Listing");
        var third = await RunAsync();

        third.Sent.Should().Be(1);
        var digest = _factory.EmailSender.AlertDigests.Should().ContainSingle().Which;
        digest.Items.Should().ContainSingle();
        digest.Items[0].Title.Should().Be("Brand New Listing");

        (await GetAlertAsync(alertId)).LastSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Paused_Alert_Is_Skipped()
    {
        await ResetEngineStateAsync();

        var (userId, _) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);

        var alertId = await SeedAlertAsync(userId, city, isActive: false);

        var result = await RunAsync();

        result.Considered.Should().Be(0);
        result.Sent.Should().Be(0);
        _factory.EmailSender.AlertDigests.Should().BeEmpty();
        (await GetAlertAsync(alertId)).LastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task Daily_Alert_Sent_Recently_Is_Not_Due()
    {
        await ResetEngineStateAsync();

        var (userId, _) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);

        var stamp = DateTimeOffset.UtcNow.AddHours(-2);
        var alertId = await SeedAlertAsync(
            userId, city, frequency: AlertFrequency.Daily, lastSentAt: stamp);

        var result = await RunAsync();

        result.Sent.Should().Be(0);
        var alert = await GetAlertAsync(alertId);
        alert.LastSentAt.Should().BeCloseTo(stamp, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Failed_Send_Does_Not_Stamp_And_Does_Not_Abort_The_Batch()
    {
        await ResetEngineStateAsync();

        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);

        var (failUserId, failEmail) = await SeedRenterAsync();
        var (okUserId, okEmail) = await SeedRenterAsync();

        var failingAlertId = await SeedAlertAsync(failUserId, city);
        var okAlertId = await SeedAlertAsync(okUserId, city);

        _factory.EmailSender.ThrowForDigestTo = failEmail;

        var result = await RunAsync();

        result.Failed.Should().Be(1);
        result.Sent.Should().Be(1);

        // The healthy neighbour still got its digest and its stamp...
        _factory.EmailSender.AlertDigests.Should().Contain(d => d.ToEmail == okEmail);
        (await GetAlertAsync(okAlertId)).LastSentAt.Should().NotBeNull();

        // ...while the failed one stays unstamped so the next run retries it.
        (await GetAlertAsync(failingAlertId)).LastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task French_Alert_Produces_A_French_Digest()
    {
        await ResetEngineStateAsync();

        var (userId, email) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);

        await SeedAlertAsync(userId, city, locale: "fr");

        await RunAsync();

        var digest = _factory.EmailSender.AlertDigests
            .Should().ContainSingle(d => d.ToEmail == email).Which;
        digest.Locale.Should().Be("fr");
        digest.Items[0].Url.Should().Contain("/fr/");
        digest.ManageAlertsUrl.Should().EndWith("/fr/renter/alerts");
    }

    [Fact]
    public async Task Run_Does_Not_Pollute_PopularSearches()
    {
        // Anti-regression for the reason the engine has its own matcher instead of reusing
        // SearchHandler: that handler records every query for the admin dashboard, so an
        // hourly engine driving it would fabricate a phantom search per alert per run.
        await ResetEngineStateAsync();

        var (userId, _) = await SeedRenterAsync();
        var city = await SeedRealCityAsync();
        await SeedPropertyAsync(city);
        await SeedAlertAsync(userId, city);

        int before;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            before = await db.PopularSearches.CountAsync();
        }

        var result = await RunAsync();
        result.Sent.Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.PopularSearches.CountAsync()).Should().Be(before);
        }
    }

    // ---- helpers -------------------------------------------------------------

    private async Task<DigestRunResult> RunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IAlertDigestService>();
        return await engine.RunAsync();
    }

    private async Task<Alert> GetAlertAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Alerts.AsNoTracking().FirstAsync(a => a.Id == id);
    }

    /// <summary>
    /// A real Cities row is required: the engine drops matches whose city has no slug,
    /// because the detail page 404s on a slug it cannot resolve.
    /// </summary>
    private async Task<City> SeedRealCityAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = Guid.NewGuid().ToString("N")[..8];
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = $"Engineville{token}",
            Slug = $"engineville{token}",
            Province = "ON",
            Latitude = 43.65,
            Longitude = -79.38
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();
        return city;
    }

    private async Task<(Guid UserId, string Email)> SeedRenterAsync()
    {
        var email = $"digest-renter+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        return (user.Id, email);
    }

    private async Task<Guid> SeedAlertAsync(
        Guid userId,
        City city,
        string? name = null,
        string locale = "en",
        AlertFrequency frequency = AlertFrequency.Instant,
        bool isActive = true,
        DateTimeOffset? lastSentAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Locale = locale,
            City = city.Name,
            Frequency = frequency,
            IsActive = isActive,
            LastSentAt = lastSentAt,
            // Backdated so freshly seeded properties fall inside the window.
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    private async Task<Guid> SeedPropertyAsync(
        City city,
        string title = "Digest Test Listing",
        decimal price = 2000)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = $"digest-landlord+{Guid.NewGuid():N}@test.local",
            UserName = $"digest-landlord+{Guid.NewGuid():N}@test.local",
            FullName = "Digest Landlord",
            EmailConfirmed = true
        };
        db.Users.Add(landlord);
        db.LandlordProfiles.Add(new LandlordProfile { Id = landlord.Id, Tier = ListingTier.Limited });

        var propId = Guid.NewGuid();
        db.Properties.Add(new Property
        {
            Id = propId,
            LandlordProfileId = landlord.Id,
            Title = title,
            Slug = $"digest-{propId:N}",
            City = city.Name,
            Province = city.Province,
            PostalCode = "M5V 1A1",
            StreetAddress = "1 Digest St",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited,
            PetsAllowed = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.Units.Add(new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = propId,
            Price = price,
            Bedrooms = 2,
            Bathrooms = 1m
        });

        await db.SaveChangesAsync();
        return propId;
    }
}
