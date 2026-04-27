using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Tests.Fixtures;

internal static class TestAuth
{
    public const string DefaultPassword = "Harness1234!";

    public static async Task<ApplicationUser> CreateUserAsync(
        RentAppFactory factory, string email, string role, string fullName = "Test User")
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = fullName,
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(user, DefaultPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Failed to create test user: " + string.Join(", ", created.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    public static async Task<HttpClient> SignInAsync(RentAppFactory factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", password),
            new KeyValuePair<string, string>("Input.RememberMe", "false")
        });
        var resp = await client.PostAsync("/login", form);
        if (resp.StatusCode != System.Net.HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Login failed with status {resp.StatusCode}");
        return client;
    }

    public static async Task<HttpClient> CreateAndSignInAsync(
        RentAppFactory factory, string role, string emailPrefix)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(factory, email, role);
        return await SignInAsync(factory, email, DefaultPassword);
    }
}
