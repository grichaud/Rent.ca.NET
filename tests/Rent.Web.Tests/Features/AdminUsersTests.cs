using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class AdminUsersTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public AdminUsersTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ToggleAdmin_GrantsAdminToRenter()
    {
        // Two distinct admins exist so the safety check on revoke would not interfere
        // (though here we add, not remove). We seed an extra admin to prove neutrality.
        await TestAuth.CreateUserAsync(_factory, $"safety-admin+{Guid.NewGuid():N}@test.local", Roles.Admin);

        var renter = await TestAuth.CreateUserAsync(_factory, $"to-promote+{Guid.NewGuid():N}@test.local", Roles.Renter);
        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "users-grant");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("UserId", renter.Id.ToString())
        });
        var response = await client.PostAsync("/en/admin/users?handler=ToggleAdmin", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = await um.FindByIdAsync(renter.Id.ToString());
        (await um.IsInRoleAsync(refreshed!, Roles.Admin)).Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAdmin_RevokesAdminWhenAnotherAdminExists()
    {
        // Seed two admins, revoke role from one. Allowed because the other still holds it.
        var adminA = await TestAuth.CreateUserAsync(_factory, $"admin-a+{Guid.NewGuid():N}@test.local", Roles.Admin);
        await TestAuth.CreateUserAsync(_factory, $"admin-b+{Guid.NewGuid():N}@test.local", Roles.Admin);

        var client = await TestAuth.CreateAndSignInAsync(_factory, Roles.Admin, "users-revoke");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("UserId", adminA.Id.ToString())
        });
        var response = await client.PostAsync("/en/admin/users?handler=ToggleAdmin", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = await um.FindByIdAsync(adminA.Id.ToString());
        (await um.IsInRoleAsync(refreshed!, Roles.Admin)).Should().BeFalse();
    }

    [Fact]
    public async Task ToggleAdmin_BlockedWhenWouldLeaveZeroAdmins()
    {
        // Set up: ensure exactly one admin (clear out all admins from the shared SQLite fixture
        // first by demoting them all to a benign role, then create a single admin to be the target).
        // Simpler approach: create our own admin and then delete all OTHER admins from this fixture
        // before running the test. But shared fixture state is risky — instead the test creates a
        // new admin AND ensures any pre-existing admins are removed for the duration of this assertion.
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Demote every existing admin so the test can drive the count to exactly 1.
        var existingAdmins = await um.GetUsersInRoleAsync(Roles.Admin);
        foreach (var ex in existingAdmins)
        {
            await um.RemoveFromRoleAsync(ex, Roles.Admin);
        }

        var soleAdmin = await TestAuth.CreateUserAsync(_factory, $"sole-admin+{Guid.NewGuid():N}@test.local", Roles.Admin);

        var client = await TestAuth.SignInAsync(_factory, soleAdmin.Email!, TestAuth.DefaultPassword);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("UserId", soleAdmin.Id.ToString())
        });
        var response = await client.PostAsync("/en/admin/users?handler=ToggleAdmin", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        var refreshed = await um.FindByIdAsync(soleAdmin.Id.ToString());
        (await um.IsInRoleAsync(refreshed!, Roles.Admin)).Should().BeTrue(
            because: "the safety check should refuse to drop the last admin");
    }
}
