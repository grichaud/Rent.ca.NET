using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Features.AiChat.Services;
using Rent.Web.Features.Email;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Data.Seed;
using Rent.Web.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Rent.Web.Tests.Fixtures;

public class RentAppFactory : WebApplicationFactory<Program>
{
    /// <summary>Shared secret the dispatch endpoint expects in tests.</summary>
    public const string TestDispatchToken = "test-dispatch-token";

    private readonly SqliteConnection _connection;

    public FakeEmailSender EmailSender { get; } = new();
    public FakeOpenRouterClient OpenRouter { get; } = new();

    static RentAppFactory()
    {
        // Set BEFORE Program.Main runs, so the startup connection-string check passes.
        // Real DbContext config is swapped out in ConfigureWebHost.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=:memory:");

        // Google OAuth dummy creds so AddGoogle() registers the scheme and the
        // "Continue with Google" button renders. Real OAuth handshake is never invoked in tests.
        Environment.SetEnvironmentVariable("Authentication__Google__ClientId", "test-google-client-id");
        Environment.SetEnvironmentVariable("Authentication__Google__ClientSecret", "test-google-client-secret");
    }

    public RentAppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:", // replaced below

                // Alert engine: no inter-send throttle in tests (the 600ms production default
                // exists only to stay under Resend's rate limit), and a known dispatch token
                // so the endpoint is mapped and its auth can be exercised.
                ["Alerts:SendDelayMs"] = "0",
                ["Alerts:DispatchToken"] = TestDispatchToken,
                ["Email:PublicBaseUrl"] = "https://rent-test.local"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IOpenRouterClient>();
            services.AddSingleton<IOpenRouterClient>(OpenRouter);

            services.RemoveAll<IAntiforgery>();
            services.AddSingleton<IAntiforgery, NoOpAntiforgery>();

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in Roles.All)
            {
                if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                    roleManager.CreateAsync(new IdentityRole<Guid>(role)).GetAwaiter().GetResult();
            }

            CitiesSeeder.SeedAsync(db).GetAwaiter().GetResult();
            AmenitiesSeeder.SeedAsync(db).GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        var descriptors = services.Where(s => s.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }
}
