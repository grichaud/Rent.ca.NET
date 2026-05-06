using System.Net;
using FluentAssertions;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class HomeTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public HomeTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_Returns200_WithFeaturedCities()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Toronto");
        body.Should().Contain("Montreal");
    }

    [Fact]
    public async Task LandlordsLanding_Returns200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/landlords");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rent.ca");
    }

    [Fact]
    public async Task CityNotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/nosuchcity");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
