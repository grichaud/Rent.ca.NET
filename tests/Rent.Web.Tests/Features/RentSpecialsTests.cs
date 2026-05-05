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

public class RentSpecialsTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public RentSpecialsTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminCreate_ValidRequest_InsertsRow()
    {
        var propId = await SeedPropertyAsync();
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "specials-create");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("PropertyId", propId.ToString()),
            new KeyValuePair<string, string>("Title", "First month free!"),
            new KeyValuePair<string, string>("Description", "Sign 12-month lease."),
            new KeyValuePair<string, string>("IsActive", "true")
        });
        var response = await client.PostAsync("/en/admin/specials?handler=Create", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inserted = await db.RentSpecials.AsNoTracking().FirstOrDefaultAsync(s => s.PropertyId == propId);
        inserted.Should().NotBeNull();
        inserted!.Title.Should().Be("First month free!");
        inserted.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AdminUpdate_ChangesTitleAndDescription()
    {
        var propId = await SeedPropertyAsync();
        var specialId = await SeedSpecialAsync(propId, "Old title", isActive: true);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "specials-update");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", specialId.ToString()),
            new KeyValuePair<string, string>("Title", "New title"),
            new KeyValuePair<string, string>("Description", "Refreshed copy"),
            new KeyValuePair<string, string>("IsActive", "true")
        });
        var response = await client.PostAsync("/en/admin/specials?handler=Update", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await db.RentSpecials.AsNoTracking().FirstAsync(s => s.Id == specialId);
        refreshed.Title.Should().Be("New title");
        refreshed.Description.Should().Be("Refreshed copy");
    }

    [Fact]
    public async Task AdminDelete_DefaultSoftDelete_MarksInactive()
    {
        var propId = await SeedPropertyAsync();
        var specialId = await SeedSpecialAsync(propId, "Soft target", isActive: true);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "specials-soft");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", specialId.ToString())
        });
        var response = await client.PostAsync("/en/admin/specials?handler=Delete", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await db.RentSpecials.AsNoTracking().FirstAsync(s => s.Id == specialId);
        refreshed.IsActive.Should().BeFalse(because: "default delete is soft (sets IsActive=false)");
    }

    [Fact]
    public async Task AdminDelete_HardFlag_RemovesRow()
    {
        var propId = await SeedPropertyAsync();
        var specialId = await SeedSpecialAsync(propId, "Hard target", isActive: false);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "specials-hard");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", specialId.ToString())
        });
        var response = await client.PostAsync("/en/admin/specials?handler=Delete&hard=true", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RentSpecials.AsNoTracking().AnyAsync(s => s.Id == specialId)).Should().BeFalse();
    }

    [Fact]
    public async Task DetailPage_RendersBanner_WhenActiveSpecialExists()
    {
        var propId = await SeedPropertyAsync(citySlug: "toronto", citySlugProvided: true, slug: "banner-on", title: "BannerOnTitle");
        await SeedSpecialAsync(propId, "Move-in bonus", isActive: true);

        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/en/toronto/banner-on");

        html.Should().Contain("Move-in bonus");
        html.Should().Contain("Special offer");
    }

    [Fact]
    public async Task DetailPage_DoesNotRenderBanner_WhenSpecialInactive()
    {
        var propId = await SeedPropertyAsync(citySlug: "toronto", citySlugProvided: true, slug: "banner-off", title: "BannerOffTitle");
        await SeedSpecialAsync(propId, "Hidden bonus", isActive: false);

        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/en/toronto/banner-off");

        html.Should().NotContain("Hidden bonus");
    }

    [Fact]
    public async Task SearchHandler_ProjectsActiveSpecialTitleOntoCard()
    {
        var propId = await SeedPropertyAsync(citySlug: "toronto", citySlugProvided: true, slug: "chip-on", title: "ChipOnTitle");
        await SeedSpecialAsync(propId, "Chip bonus", isActive: true);

        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<Rent.Web.Features.Search.SearchHandler>();

        var result = await handler.ExecuteAsync(new Rent.Web.Features.Search.SearchQuery
        {
            CitySlug = "toronto"
        });

        var card = result.Properties.SingleOrDefault(p => p.Id == propId);
        card.Should().NotBeNull();
        card!.SpecialTitle.Should().Be("Chip bonus");
    }

    private async Task<Guid> SeedPropertyAsync(
        string citySlug = "",
        bool citySlugProvided = false,
        string? slug = null,
        string? title = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var landlordEmail = $"specials-{Guid.NewGuid():N}@test.local";
        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = landlordEmail,
            UserName = landlordEmail,
            FullName = "Specials Landlord",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(landlord, "Landlord1234!");
        await userManager.AddToRoleAsync(landlord, Roles.Landlord);
        db.LandlordProfiles.Add(new LandlordProfile { Id = landlord.Id, CompanyName = "Specials Co" });
        await db.SaveChangesAsync();

        // If a city slug is provided we look up its display name; otherwise default to "Toronto"
        // (already seeded by the fixture). Property.City stores the display name, not the slug.
        string cityName = "Toronto";
        if (citySlugProvided && !string.IsNullOrEmpty(citySlug))
        {
            var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == citySlug);
            if (city is null)
            {
                throw new InvalidOperationException($"City '{citySlug}' not seeded by fixture.");
            }
            cityName = city.Name;
        }

        var prop = new Property
        {
            LandlordProfileId = landlord.Id,
            Title = title ?? $"Specials demo {Guid.NewGuid():N}",
            Slug = slug ?? $"specials-{Guid.NewGuid():N}",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited,
            StreetAddress = "1 Specials Way",
            City = cityName,
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

    private async Task<Guid> SeedSpecialAsync(Guid propertyId, string title, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var special = new RentSpecial
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            Title = title,
            Description = "Test description",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.RentSpecials.Add(special);
        await db.SaveChangesAsync();
        return special.Id;
    }
}
