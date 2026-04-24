using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Features.Search;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

// Note: these tests hit the full handler which uses decimal Min aggregates and
// collection includes. SQLite (used by the test fixture) cannot translate
// decimal aggregates; only city-resolution behavior is covered in-process here.
// Full search/filter validation runs against the live SQL Server database
// during manual and Playwright E2E verification.
public class SearchHandlerTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public SearchHandlerTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_UnknownCity_ReturnsEmptyResult()
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SearchHandler>();

        var result = await handler.ExecuteAsync(new SearchQuery { CitySlug = "atlantis" });

        result.City.Should().BeNull();
        result.Properties.Should().BeEmpty();
    }
}
