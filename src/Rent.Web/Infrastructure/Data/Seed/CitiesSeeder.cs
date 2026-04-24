using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;

namespace Rent.Web.Infrastructure.Data.Seed;

public static class CitiesSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Cities.AnyAsync(ct)) return;

        var cities = new[]
        {
            New("Toronto",    "ON", featured: true,  35.6762, -79.3832, "/images/cities/toronto.jpg"),
            New("Montreal",   "QC", featured: true,  45.5017, -73.5673, "/images/cities/montreal.jpg"),
            New("Vancouver",  "BC", featured: true,  49.2827, -123.1207, "/images/cities/vancouver.jpg"),
            New("Calgary",    "AB", featured: true,  51.0447, -114.0719, "/images/cities/calgary.jpg"),
            New("Ottawa",     "ON", featured: true,  45.4215, -75.6972, "/images/cities/ottawa.jpg"),
            New("Edmonton",   "AB", featured: true,  53.5461, -113.4938, "/images/cities/edmonton.jpg"),
            New("Winnipeg",   "MB", featured: false, 49.8951,  -97.1384),
            New("Quebec City","QC", featured: false, 46.8139,  -71.2080),
            New("Hamilton",   "ON", featured: false, 43.2557,  -79.8711),
            New("Kitchener",  "ON", featured: false, 43.4516,  -80.4925),
            New("London",     "ON", featured: false, 42.9849,  -81.2453),
            New("Victoria",   "BC", featured: false, 48.4284, -123.3656),
            New("Halifax",    "NS", featured: false, 44.6488,  -63.5752),
            New("Oshawa",     "ON", featured: false, 43.8971,  -78.8658),
            New("Windsor",    "ON", featured: false, 42.3149,  -83.0364),
            New("Saskatoon",  "SK", featured: false, 52.1332, -106.6700),
            New("Regina",     "SK", featured: false, 50.4452, -104.6189),
            New("St. John's", "NL", featured: false, 47.5615,  -52.7126),
            New("Barrie",     "ON", featured: false, 44.3894,  -79.6903),
            New("Kelowna",    "BC", featured: false, 49.8880, -119.4960),
            New("Sherbrooke", "QC", featured: false, 45.4040,  -71.8929),
            New("Guelph",     "ON", featured: false, 43.5448,  -80.2482),
            New("Abbotsford", "BC", featured: false, 49.0504, -122.3045),
            New("Kingston",   "ON", featured: false, 44.2312,  -76.4860),
            New("Trois-Rivieres","QC", featured: false, 46.3432, -72.5432),
            New("Moncton",    "NB", featured: false, 46.0878,  -64.7782),
            New("Saguenay",   "QC", featured: false, 48.4168,  -71.0650),
            New("Burnaby",    "BC", featured: false, 49.2488, -122.9805),
            New("Mississauga","ON", featured: false, 43.5890,  -79.6441),
            New("Brampton",   "ON", featured: false, 43.7315,  -79.7624)
        };

        db.Cities.AddRange(cities);
        await db.SaveChangesAsync(ct);
    }

    private static City New(string name, string province, bool featured, double lat, double lng, string? image = null)
    {
        return new City
        {
            Name = name,
            Province = province,
            Slug = Slugify(name),
            IsFeatured = featured,
            Latitude = lat,
            Longitude = lng,
            ImageUrl = image,
            ListingCount = 0
        };
    }

    private static string Slugify(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace("'", "")
            .Replace(".", "")
            .Replace(" ", "-");
    }
}
