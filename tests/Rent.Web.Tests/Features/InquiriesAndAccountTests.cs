using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class InquiriesAndAccountTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public InquiriesAndAccountTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Inquiries_ShowsOnlySenderInquiries()
    {
        var senderEmail = $"inq-sender+{Guid.NewGuid():N}@test.local";
        var sender = await TestAuth.CreateUserAsync(_factory, senderEmail, Roles.Renter);
        var otherEmail = $"inq-other+{Guid.NewGuid():N}@test.local";
        var other = await TestAuth.CreateUserAsync(_factory, otherEmail, Roles.Renter);

        Guid propertyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var landlord = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = $"landlord-inq+{Guid.NewGuid():N}@test.local",
                UserName = $"landlord-inq+{Guid.NewGuid():N}@test.local",
                FullName = "Inq Landlord",
                EmailConfirmed = true
            };
            db.Users.Add(landlord);
            db.LandlordProfiles.Add(new LandlordProfile { Id = landlord.Id, Tier = ListingTier.Limited });
            propertyId = Guid.NewGuid();
            db.Properties.Add(new Property
            {
                Id = propertyId,
                LandlordProfileId = landlord.Id,
                Title = "Inq Test Property",
                Slug = $"inq-test-{Guid.NewGuid():N}",
                City = "Toronto",
                Province = "ON",
                PostalCode = "M5V 1A1",
                StreetAddress = "1 Inq St",
                PropertyType = PropertyType.Apartment,
                Status = ListingStatus.Active,
                Tier = ListingTier.Limited
            });
            db.ContactInquiries.Add(new ContactInquiry
            {
                PropertyId = propertyId,
                SenderUserId = sender.Id,
                SenderName = "Sender Self",
                SenderEmail = senderEmail,
                Message = "Mine should appear",
                IsRead = false
            });
            db.ContactInquiries.Add(new ContactInquiry
            {
                PropertyId = propertyId,
                SenderUserId = null,
                SenderName = "Anonymous",
                SenderEmail = "anon@test.local",
                Message = "From anon",
                IsRead = false
            });
            db.ContactInquiries.Add(new ContactInquiry
            {
                PropertyId = propertyId,
                SenderUserId = other.Id,
                SenderName = "Other User",
                SenderEmail = otherEmail,
                Message = "From other user",
                IsRead = false
            });
            await db.SaveChangesAsync();
        }

        using var client = await TestAuth.SignInAsync(_factory, senderEmail, TestAuth.DefaultPassword);
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        var resp = await client.GetAsync("/en/renter/inquiries");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Mine should appear");
        body.Should().NotContain("From anon");
        body.Should().NotContain("From other user");
    }

    [Fact]
    public async Task Account_UpdateFullName_Persists()
    {
        var email = $"account-name+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Renter, "Old Name");
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        var resp = await client.PostAsync("/en/renter/account?handler=Profile",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Profile.FullName", "New Renter Name")
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = await um.FindByIdAsync(user.Id.ToString());
        refreshed!.FullName.Should().Be("New Renter Name");
    }

    [Fact]
    public async Task Account_ChangePassword_Succeeds_WithCurrent()
    {
        var email = $"account-pw+{Guid.NewGuid():N}@test.local";
        await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        var resp = await client.PostAsync("/en/renter/account?handler=Password",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Password.CurrentPassword", TestAuth.DefaultPassword),
                new KeyValuePair<string, string>("Password.NewPassword", "NewPass1234!"),
                new KeyValuePair<string, string>("Password.ConfirmPassword", "NewPass1234!")
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Confirm we can log in with the new password
        using var fresh = await TestAuth.SignInAsync(_factory, email, "NewPass1234!");
    }

    [Fact]
    public async Task Account_ChangePassword_Fails_WithWrongCurrent()
    {
        var email = $"account-pw-bad+{Guid.NewGuid():N}@test.local";
        await TestAuth.CreateUserAsync(_factory, email, Roles.Renter);
        using var client = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);

        var resp = await client.PostAsync("/en/renter/account?handler=Password",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Password.CurrentPassword", "WrongCurrent1!"),
                new KeyValuePair<string, string>("Password.NewPassword", "AnotherPass1!"),
                new KeyValuePair<string, string>("Password.ConfirmPassword", "AnotherPass1!")
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().ContainAny("Incorrect", "incorrect", "current password");
    }

    [Fact]
    public async Task Account_GoogleUser_NoPasswordSection()
    {
        // Simulate a Google OAuth user — created without a password.
        var email = $"google-user+{Guid.NewGuid():N}@test.local";
        ApplicationUser googleUser;
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            googleUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                FullName = "Google Renter",
                EmailConfirmed = true
            };
            var created = await um.CreateAsync(googleUser);
            created.Succeeded.Should().BeTrue();
            await um.AddToRoleAsync(googleUser, Roles.Renter);
            await um.AddLoginAsync(googleUser, new UserLoginInfo("Google", $"google-{Guid.NewGuid():N}", "Google"));
        }

        // Cannot use TestAuth.SignInAsync (no password). Use SignInManager via a custom helper:
        // For this test we just verify the page renders the message when a user without password
        // hits /renter/account. We bypass the login by signing in via UserManager + cookie.
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Set a temporary password so we can log in, then remove it to mimic Google-only user.
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = await um.FindByIdAsync(googleUser.Id.ToString());
            await um.AddPasswordAsync(u!, TestAuth.DefaultPassword);
        }
        using var loggedIn = await TestAuth.SignInAsync(_factory, email, TestAuth.DefaultPassword);
        // Now strip the password so the page sees "no local password"
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = await um.FindByIdAsync(googleUser.Id.ToString());
            await um.RemovePasswordAsync(u!);
        }
        loggedIn.DefaultRequestHeaders.Add("Accept", "text/html");
        var resp = await loggedIn.GetAsync("/en/renter/account");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("managed by Google");
    }
}
