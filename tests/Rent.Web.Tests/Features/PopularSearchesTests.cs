using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Features.Admin.Services;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class PopularSearchesTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public PopularSearchesTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TrackInline_FirstCall_InsertsRowWithCountOne()
    {
        var citySlug = $"trk-{Guid.NewGuid():N}".Substring(0, 16);
        var query = $"bedrooms={Guid.NewGuid():N}".Substring(0, 24);

        using var scope = _factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
        await tracker.TrackInlineAsync(query, citySlug);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.PopularSearches.AsNoTracking()
            .FirstAsync(s => s.NormalizedQuery == query.ToLowerInvariant() && s.CitySlug == citySlug);
        row.SearchCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackInline_SecondCallSameQuery_IncrementsToTwo()
    {
        var citySlug = $"trk-{Guid.NewGuid():N}".Substring(0, 16);
        var query = $"bedrooms={Guid.NewGuid():N}".Substring(0, 24);

        using var scope = _factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
        await tracker.TrackInlineAsync(query, citySlug);
        await tracker.TrackInlineAsync(query, citySlug);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.PopularSearches.AsNoTracking()
            .FirstAsync(s => s.NormalizedQuery == query.ToLowerInvariant() && s.CitySlug == citySlug);
        row.SearchCount.Should().Be(2);
    }

    [Fact]
    public async Task TrackInline_EmptyQuery_StoresEmptySentinel()
    {
        var citySlug = $"trk-{Guid.NewGuid():N}".Substring(0, 16);

        using var scope = _factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
        await tracker.TrackInlineAsync(string.Empty, citySlug);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.PopularSearches.AsNoTracking()
            .FirstAsync(s => s.CitySlug == citySlug);
        row.NormalizedQuery.Should().Be("(empty)");
    }

    [Fact]
    public async Task AdminSearchesPage_ListsTopEntries()
    {
        var query = $"toplist-{Guid.NewGuid():N}".Substring(0, 24);
        var citySlug = $"top-{Guid.NewGuid():N}".Substring(0, 16);

        using (var seedScope = _factory.Services.CreateScope())
        {
            var tracker = seedScope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
            await tracker.TrackInlineAsync(query, citySlug);
            await tracker.TrackInlineAsync(query, citySlug);
        }

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "searches-list");
        var html = await client.GetStringAsync("/en/admin/searches");

        html.Should().Contain(query.ToLowerInvariant());
        html.Should().Contain(citySlug);
    }

    [Fact]
    public async Task AdminEdit_ChangesNormalizedQuery()
    {
        var citySlug = $"edt-{Guid.NewGuid():N}".Substring(0, 16);
        var oldQuery = $"old-{Guid.NewGuid():N}".Substring(0, 24);
        var newQuery = $"new-{Guid.NewGuid():N}".Substring(0, 24);
        Guid id;

        using (var seedScope = _factory.Services.CreateScope())
        {
            var tracker = seedScope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
            await tracker.TrackInlineAsync(oldQuery, citySlug);

            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = (await db.PopularSearches.AsNoTracking()
                .FirstAsync(s => s.NormalizedQuery == oldQuery.ToLowerInvariant() && s.CitySlug == citySlug)).Id;
        }

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "searches-edit");
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", id.ToString()),
            new KeyValuePair<string, string>("NormalizedQuery", newQuery),
            new KeyValuePair<string, string>("CitySlug", citySlug)
        });
        var response = await client.PostAsync("/en/admin/searches?handler=Update", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await verifyDb.PopularSearches.AsNoTracking().FirstAsync(s => s.Id == id);
        refreshed.NormalizedQuery.Should().Be(newQuery.ToLowerInvariant());
    }

    [Fact]
    public async Task AdminEdit_UniqueConflict_RejectsAndPreservesOldValue()
    {
        var citySlug = $"cnf-{Guid.NewGuid():N}".Substring(0, 16);
        var queryA = $"a-{Guid.NewGuid():N}".Substring(0, 24);
        var queryB = $"b-{Guid.NewGuid():N}".Substring(0, 24);
        Guid idA;

        using (var seedScope = _factory.Services.CreateScope())
        {
            var tracker = seedScope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
            await tracker.TrackInlineAsync(queryA, citySlug);
            await tracker.TrackInlineAsync(queryB, citySlug);

            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            idA = (await db.PopularSearches.AsNoTracking()
                .FirstAsync(s => s.NormalizedQuery == queryA.ToLowerInvariant() && s.CitySlug == citySlug)).Id;
        }

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "searches-conflict");
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", idA.ToString()),
            new KeyValuePair<string, string>("NormalizedQuery", queryB),
            new KeyValuePair<string, string>("CitySlug", citySlug)
        });
        var response = await client.PostAsync("/en/admin/searches?handler=Update", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = await verifyDb.PopularSearches.AsNoTracking().FirstAsync(s => s.Id == idA);
        refreshed.NormalizedQuery.Should().Be(queryA.ToLowerInvariant(),
            because: "unique index conflict should be caught and original preserved");
    }

    [Fact]
    public async Task AdminDelete_RemovesRow()
    {
        var citySlug = $"del-{Guid.NewGuid():N}".Substring(0, 16);
        var query = $"del-{Guid.NewGuid():N}".Substring(0, 24);
        Guid id;

        using (var seedScope = _factory.Services.CreateScope())
        {
            var tracker = seedScope.ServiceProvider.GetRequiredService<IPopularSearchTracker>();
            await tracker.TrackInlineAsync(query, citySlug);

            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = (await db.PopularSearches.AsNoTracking()
                .FirstAsync(s => s.NormalizedQuery == query.ToLowerInvariant())).Id;
        }

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "searches-delete");
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", id.ToString())
        });
        var response = await client.PostAsync("/en/admin/searches?handler=Delete", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.PopularSearches.AsNoTracking().AnyAsync(s => s.Id == id)).Should().BeFalse();
    }
}
