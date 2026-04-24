using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Infrastructure.Data.Seed;

public static class DatabaseSeeder
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        await CitiesSeeder.SeedAsync(db, ct);
        await AmenitiesSeeder.SeedAsync(db, ct);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Domain.ApplicationUser>>();
        await SamplePropertiesSeeder.SeedAsync(db, userManager, ct);
    }
}
