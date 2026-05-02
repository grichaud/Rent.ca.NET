using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.AiChat.Tools;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AiToolsTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AiToolsTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchPropertiesTool_ExecutesQuery_ReturnsTop5()
    {
        await SeedDemoPropertyAsync("Toronto", PropertyType.Apartment, 2200m, bedrooms: 2);
        await SeedDemoPropertyAsync("Toronto", PropertyType.Apartment, 1800m, bedrooms: 1);
        await SeedDemoPropertyAsync("Vancouver", PropertyType.Condo, 2400m, bedrooms: 1);

        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchPropertiesTool>();
        var args = JsonSerializer.Serialize(new { city = "Toronto", price_max = 2500, bedrooms = 1 });

        var result = await tool.ExecuteAsync(args, new ToolExecutionContext(null, Guid.NewGuid()));

        result.Success.Should().BeTrue();
        var json = JsonSerializer.Serialize(result.Data);
        json.Should().Contain("Toronto");
        json.Should().NotContain("Vancouver");
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("count").GetInt32().Should().BeGreaterThan(0).And.BeLessOrEqualTo(5);
        doc.RootElement.GetProperty("properties").EnumerateArray().First().GetProperty("url").GetString()
            .Should().StartWith("/toronto/");
    }

    [Fact]
    public async Task CreateAlertTool_RequiresLogin_ForAnonymous()
    {
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<CreateAlertTool>();

        var result = await tool.ExecuteAsync(
            JsonSerializer.Serialize(new { city = "Calgary", pets_allowed = true }),
            new ToolExecutionContext(UserId: null, SessionId: Guid.NewGuid()));

        result.Success.Should().BeFalse();
        var json = JsonSerializer.Serialize(result.Data);
        json.Should().Contain("requiresLogin");
    }

    [Fact]
    public async Task CreateAlertTool_PersistsAlert_ForLoggedInUser()
    {
        var email = $"ai-alert+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);

        Guid alertId;
        using (var scope = _factory.Services.CreateScope())
        {
            var tool = scope.ServiceProvider.GetRequiredService<CreateAlertTool>();
            var result = await tool.ExecuteAsync(
                JsonSerializer.Serialize(new { city = "Calgary", price_max = 2000, pets_allowed = true }),
                new ToolExecutionContext(UserId: user.Id, SessionId: Guid.NewGuid()));

            result.Success.Should().BeTrue();
            var json = JsonSerializer.Serialize(result.Data);
            var doc = JsonDocument.Parse(json);
            alertId = doc.RootElement.GetProperty("alertId").GetGuid();
            doc.RootElement.GetProperty("summary").GetString().Should().Contain("Calgary");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Alerts.AsNoTracking().FirstAsync(a => a.Id == alertId);
            saved.UserId.Should().Be(user.Id);
            saved.City.Should().Be("Calgary");
            saved.PriceMax.Should().Be(2000m);
            saved.PetsAllowed.Should().BeTrue();
            saved.IsActive.Should().BeTrue();
            saved.Frequency.Should().Be(AlertFrequency.Daily);
        }
    }

    [Fact]
    public async Task GetCityInfoTool_FindsCityByPartialName()
    {
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetCityInfoTool>();

        var result = await tool.ExecuteAsync(
            JsonSerializer.Serialize(new { city = "toronto" }),
            new ToolExecutionContext(null, Guid.NewGuid()));

        result.Success.Should().BeTrue();
        var json = JsonSerializer.Serialize(result.Data);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("found").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("name").GetString().Should().Be("Toronto");
        doc.RootElement.GetProperty("province").GetString().Should().Be("ON");
        doc.RootElement.GetProperty("slug").GetString().Should().Be("toronto");
    }

    [Fact]
    public async Task GetPropertyDetailsTool_ReturnsFullDetails()
    {
        var propertyId = await SeedDemoPropertyAsync("Ottawa", PropertyType.Townhouse, 2100m, bedrooms: 3);

        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetPropertyDetailsTool>();

        var result = await tool.ExecuteAsync(
            JsonSerializer.Serialize(new { property_id = propertyId.ToString() }),
            new ToolExecutionContext(null, Guid.NewGuid()));

        result.Success.Should().BeTrue();
        var json = JsonSerializer.Serialize(result.Data);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("found").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("city").GetString().Should().Be("Ottawa");
        doc.RootElement.GetProperty("propertyType").GetString().Should().Be("Townhouse");
        doc.RootElement.GetProperty("units").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public void ToolRegistry_BuildsValidOpenAISchema()
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ToolRegistry>();

        var schema = registry.ToOpenAISchema();
        schema.Should().HaveCount(4);
        schema.Select(t => t.Function.Name).Should().Contain(new[]
        {
            "search_properties", "create_alert", "get_city_info", "get_property_details"
        });

        foreach (var def in schema)
        {
            def.Type.Should().Be("function");
            def.Function.Name.Should().NotBeNullOrEmpty();
            def.Function.Description.Should().NotBeNullOrEmpty();

            var paramsJson = JsonSerializer.Serialize(def.Function.Parameters);
            var doc = JsonDocument.Parse(paramsJson);
            doc.RootElement.GetProperty("type").GetString().Should().Be("object");
            doc.RootElement.TryGetProperty("properties", out _).Should().BeTrue();
        }
    }

    private async Task<Guid> SeedDemoPropertyAsync(string city, PropertyType type, decimal price, int bedrooms)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string landlordEmail = "ai-tools-landlord@test.local";
        var landlord = await userManager.FindByEmailAsync(landlordEmail);
        if (landlord is null)
        {
            landlord = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = landlordEmail,
                UserName = landlordEmail,
                FullName = "AI Tools Landlord",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(landlord, "Landlord1234!");
            await userManager.AddToRoleAsync(landlord, Roles.Landlord);
        }

        var profile = await db.LandlordProfiles.FirstOrDefaultAsync(x => x.Id == landlord.Id);
        if (profile is null)
        {
            db.LandlordProfiles.Add(new LandlordProfile
            {
                Id = landlord.Id,
                CompanyName = "AI Tools Holdings",
                Tier = ListingTier.Limited
            });
            await db.SaveChangesAsync();
        }

        var slug = $"demo-{Guid.NewGuid():N}";
        var prop = new Property
        {
            LandlordProfileId = landlord.Id,
            Title = $"Demo {city} {type}",
            Slug = slug,
            PropertyType = type,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited,
            StreetAddress = "123 Test St",
            City = city,
            Province = "ON",
            PostalCode = "M0M 0M0",
            PetsAllowed = true,
            Furnished = false,
            ParkingType = "Underground"
        };
        prop.Units.Add(new Unit
        {
            Bedrooms = bedrooms,
            Bathrooms = 1m,
            Price = price,
            SqFt = 800,
            AvailableUnits = 1
        });
        db.Properties.Add(prop);
        await db.SaveChangesAsync();
        return prop.Id;
    }
}
