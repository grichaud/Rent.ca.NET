using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class EmailTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public EmailTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_DoesNotSendButReturnsGenericMessage()
    {
        _factory.EmailSender.Reset();

        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/en/forgot-password",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "nobody-" + Guid.NewGuid().ToString("N") + "@example.com"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("If an account with that email exists");
        _factory.EmailSender.PasswordResets.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsResetEmail()
    {
        _factory.EmailSender.Reset();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = "Reset User"
        };
        (await userManager.CreateAsync(user, "Original1!")).Succeeded.Should().BeTrue();

        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/en/forgot-password",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Email"] = email }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailSender.PasswordResets.Should().ContainSingle()
            .Which.ToEmail.Should().Be(email);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPassword()
    {
        _factory.EmailSender.Reset();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"resetok-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = "Reset OK"
        };
        (await userManager.CreateAsync(user, "Original1!")).Succeeded.Should().BeTrue();

        var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsync("/en/reset-password",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Token"] = encodedToken,
                ["Input.Password"] = "Replaced1!",
                ["Input.ConfirmPassword"] = "Replaced1!"
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.ToString().ToLowerInvariant().Should().Contain("/login");

        // Fresh scope so the DbContext doesn't return the cached pre-reset entity.
        using var verifyScope = _factory.Services.CreateScope();
        var freshUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = (await freshUserManager.FindByEmailAsync(email))!;
        (await freshUserManager.CheckPasswordAsync(refreshed, "Replaced1!")).Should().BeTrue();
        (await freshUserManager.CheckPasswordAsync(refreshed, "Original1!")).Should().BeFalse();
    }

    [Fact]
    public async Task Inquiry_TriggersEmailSend()
    {
        _factory.EmailSender.Reset();
        var (propertyId, landlordEmail, _) = await SeedActiveListingAsync();

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsync("/inquiries/submit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PropertyId"] = propertyId.ToString(),
                ["SenderName"] = "Curious Renter",
                ["SenderEmail"] = "renter@example.com",
                ["Message"] = "Is this still available next month?"
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        _factory.EmailSender.Inquiries.Should().ContainSingle()
            .Which.LandlordEmail.Should().Be(landlordEmail);
    }

    [Fact]
    public async Task Inquiry_StillSucceedsWhenEmailFails()
    {
        _factory.EmailSender.Reset();
        _factory.EmailSender.ShouldThrow = true;

        var (propertyId, _, _) = await SeedActiveListingAsync();

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsync("/inquiries/submit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PropertyId"] = propertyId.ToString(),
                ["SenderName"] = "Resilient Renter",
                ["SenderEmail"] = "resilient@example.com",
                ["Message"] = "Send still works even if email fails"
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        _factory.EmailSender.Inquiries.Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inquiry = await db.ContactInquiries
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.PropertyId == propertyId && i.SenderEmail == "resilient@example.com");
        inquiry.Should().NotBeNull();

        _factory.EmailSender.ShouldThrow = false;
    }

    private async Task<(Guid PropertyId, string LandlordEmail, Guid LandlordId)> SeedActiveListingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var landlordEmail = $"landlord-{Guid.NewGuid():N}@example.com";
        var landlord = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = landlordEmail,
            UserName = landlordEmail,
            FullName = "Test Landlord",
            EmailConfirmed = true
        };
        (await userManager.CreateAsync(landlord, "Landlord1!")).Succeeded.Should().BeTrue();
        await userManager.AddToRoleAsync(landlord, Roles.Landlord);

        var profile = new LandlordProfile
        {
            Id = landlord.Id,
            Tier = ListingTier.Limited
        };
        db.LandlordProfiles.Add(profile);

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordProfileId = landlord.Id,
            Title = "Bright 2br near transit",
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            Tier = ListingTier.Limited,
            StreetAddress = "1 Test St",
            City = "Toronto",
            Province = "ON",
            PostalCode = "M5H 1A1",
            Slug = "test-listing-" + Guid.NewGuid().ToString("N").Substring(0, 8)
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        return (property.Id, landlordEmail, landlord.Id);
    }
}
