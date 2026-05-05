using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;

namespace Rent.Web.Infrastructure.Data.Seed;

public static class RentSpecialsSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.RentSpecials.AnyAsync(ct)) return;

        // Anchor demo specials on two of the seeded sample properties so the banner shows
        // up immediately on the King Street loft (Featured) and the Old Montreal loft.
        var torontoLoft = await db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == "luxury-lofts-on-king-street", ct);
        var montrealLoft = await db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == "old-montreal-heritage-loft", ct);

        var now = DateTimeOffset.UtcNow;

        if (torontoLoft is not null)
        {
            db.RentSpecials.Add(new RentSpecial
            {
                Id = Guid.NewGuid(),
                PropertyId = torontoLoft.Id,
                Title = "First month free!",
                Description = "Sign a 12-month lease before the end of the month and your first month is on us. Includes parking and storage locker.",
                StartDate = now.AddDays(-7),
                EndDate = now.AddDays(60),
                IsActive = true,
                CreatedAt = now
            });
        }

        if (montrealLoft is not null)
        {
            db.RentSpecials.Add(new RentSpecial
            {
                Id = Guid.NewGuid(),
                PropertyId = montrealLoft.Id,
                Title = "Reduced security deposit",
                Description = "Half-month security deposit instead of one full month for new tenants this season.",
                StartDate = now.AddDays(-3),
                EndDate = now.AddDays(45),
                IsActive = true,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
