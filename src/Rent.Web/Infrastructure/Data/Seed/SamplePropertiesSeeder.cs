using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Infrastructure.Data.Seed;

public static class SamplePropertiesSeeder
{
    private const string DemoLandlordEmail = "demo.landlord@rentca.net";
    private const string DemoLandlordPassword = "DemoLandlord1!";

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct = default)
    {
        if (await db.Properties.AnyAsync(ct)) return;

        var landlordId = await EnsureDemoLandlordAsync(db, userManager);
        var amenities = await db.Amenities.ToDictionaryAsync(a => a.Name, ct);

        var samples = BuildSamples(landlordId);
        foreach (var (property, amenityNames) in samples)
        {
            foreach (var name in amenityNames)
            {
                if (amenities.TryGetValue(name, out var amenity))
                    property.Amenities.Add(amenity);
            }
            db.Properties.Add(property);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid> EnsureDemoLandlordAsync(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        var existing = await userManager.FindByEmailAsync(DemoLandlordEmail);
        if (existing is not null) return existing.Id;

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = DemoLandlordEmail,
            UserName = DemoLandlordEmail,
            EmailConfirmed = true,
            FullName = "Demo Properties Inc."
        };
        var result = await userManager.CreateAsync(user, DemoLandlordPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create demo landlord: " + string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, Roles.Landlord);

        db.LandlordProfiles.Add(new LandlordProfile
        {
            Id = user.Id,
            CompanyName = "Demo Properties Inc.",
            Description = "Sample listings populated by the seeder for portfolio demo purposes.",
            IsVerified = true,
            Tier = ListingTier.Featured,
            LogoUrl = "https://picsum.photos/seed/demo-landlord/80/80"
        });

        return user.Id;
    }

    private static IEnumerable<(Property property, string[] amenities)> BuildSamples(Guid landlordId)
    {
        yield return (Make(landlordId,
            title: "Luxury Lofts on King Street",
            type: PropertyType.Loft,
            street: "250 King Street West",
            city: "Toronto", province: "ON", postal: "M5V 1J2",
            neighbourhood: "Entertainment District",
            lat: 43.6466, lng: -79.3901,
            slug: "luxury-lofts-on-king-street",
            tier: ListingTier.Featured,
            description: "Soaring 12-ft ceilings, exposed brick, and floor-to-ceiling windows overlooking King West. Walk to the CN Tower, restaurants, and TIFF.",
            units: [
                new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 620, Price = 2450, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)) },
                new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 950, Price = 3600, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) }
            ],
            imageSeeds: ["toronto-loft-1", "toronto-loft-2", "toronto-loft-3", "toronto-loft-4"]),
            ["Elevator", "Gym", "In-Suite Laundry", "Hardwood Floors", "Balcony", "Underground Parking", "24/7 Security", "Pet Friendly"]);

        yield return (Make(landlordId,
            title: "Bright 2BR Near High Park",
            type: PropertyType.Apartment,
            street: "1860 Bloor Street West",
            city: "Toronto", province: "ON", postal: "M6P 1P5",
            neighbourhood: "High Park",
            lat: 43.6544, lng: -79.4673,
            slug: "bright-2br-near-high-park",
            tier: ListingTier.Promoted,
            description: "Spacious two-bedroom with parquet floors, a sun-drenched living room, and a shared rooftop garden. Steps from the subway.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 1, SqFt = 780, Price = 2800, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)) } ],
            imageSeeds: ["toronto-apt-1", "toronto-apt-2", "toronto-apt-3"]),
            ["Hardwood Floors", "In-Suite Laundry", "Cats Allowed", "Heat Included"]);

        yield return (Make(landlordId,
            title: "Charming Plateau Triplex",
            type: PropertyType.Duplex,
            street: "4550 Rue Saint-Denis",
            city: "Montreal", province: "QC", postal: "H2J 2L4",
            neighbourhood: "Le Plateau",
            lat: 45.5258, lng: -73.5809,
            slug: "charming-plateau-triplex",
            tier: ListingTier.Promoted,
            description: "Classic Montreal triplex with original mouldings, wide plank floors, and a sun-filled kitchen. Steps from Mont-Royal Avenue.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 1.5m, SqFt = 1100, Price = 2300, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)) } ],
            imageSeeds: ["mtl-1", "mtl-2", "mtl-3"]),
            ["Hardwood Floors", "Balcony", "Pet Friendly", "Fireplace"]);

        yield return (Make(landlordId,
            title: "Yaletown Skyline Condo",
            type: PropertyType.Condo,
            street: "1308 Hornby Street",
            city: "Vancouver", province: "BC", postal: "V6Z 0C4",
            neighbourhood: "Yaletown",
            lat: 49.2747, lng: -123.1207,
            slug: "yaletown-skyline-condo",
            tier: ListingTier.Featured,
            description: "Panoramic views of False Creek and the North Shore mountains. Modern finishes, in-suite laundry, and a rooftop pool.",
            units: [
                new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 580, Price = 2650, AvailableUnits = 3, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) },
                new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 880, Price = 3950, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)) }
            ],
            imageSeeds: ["van-1", "van-2", "van-3", "van-4"]),
            ["Gym", "Pool", "Concierge", "Elevator", "In-Suite Laundry", "Smart Access", "Underground Parking"]);

        yield return (Make(landlordId,
            title: "Kitsilano Beach House",
            type: PropertyType.House,
            street: "2245 Cornwall Avenue",
            city: "Vancouver", province: "BC", postal: "V6K 1B7",
            neighbourhood: "Kitsilano",
            lat: 49.2714, lng: -123.1558,
            slug: "kitsilano-beach-house",
            tier: ListingTier.Limited,
            description: "Detached 3-bedroom with a sun deck, garden, and a short walk to Kits Beach. Perfect for families.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 2, SqFt = 1650, Price = 4750, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)) } ],
            imageSeeds: ["kits-1", "kits-2"]),
            ["Fireplace", "Dogs Allowed", "Outdoor Parking"]);

        yield return (Make(landlordId,
            title: "Beltline Studio",
            type: PropertyType.Studio,
            street: "1011 12 Avenue SW",
            city: "Calgary", province: "AB", postal: "T2R 0J5",
            neighbourhood: "Beltline",
            lat: 51.0397, lng: -114.0791,
            slug: "beltline-studio",
            tier: ListingTier.Limited,
            description: "Cozy studio in the heart of the Beltline. Walking distance to 17th Ave shops and restaurants. Utilities included.",
            units: [ new Unit { Bedrooms = 0, Bathrooms = 1, SqFt = 420, Price = 1395, AvailableUnits = 4, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow) } ],
            imageSeeds: ["cal-1", "cal-2"]),
            ["Heat Included", "Water Included", "Elevator", "Gym"]);

        yield return (Make(landlordId,
            title: "ByWard Market Townhouse",
            type: PropertyType.Townhouse,
            street: "380 Dalhousie Street",
            city: "Ottawa", province: "ON", postal: "K1N 7E8",
            neighbourhood: "ByWard Market",
            lat: 45.4309, lng: -75.6927,
            slug: "byward-market-townhouse",
            tier: ListingTier.Promoted,
            description: "Three-level townhouse with private garage and rooftop patio. Steps from Parliament, restaurants, and the market.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 2.5m, SqFt = 1480, Price = 3200, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) } ],
            imageSeeds: ["ott-1", "ott-2", "ott-3"]),
            ["Fireplace", "Balcony", "EV Charging", "Air Conditioning", "In-Suite Laundry"]);

        yield return (Make(landlordId,
            title: "Oliver Square One-Bedroom",
            type: PropertyType.Apartment,
            street: "10235 104 Street NW",
            city: "Edmonton", province: "AB", postal: "T5J 1B9",
            neighbourhood: "Oliver",
            lat: 53.5456, lng: -113.4936,
            slug: "oliver-square-one-bedroom",
            tier: ListingTier.Limited,
            description: "Modern one-bedroom in downtown Edmonton with a balcony, in-suite laundry, and access to a shared fitness room.",
            units: [ new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 540, Price = 1450, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)) } ],
            imageSeeds: ["edm-1", "edm-2"]),
            ["Gym", "Elevator", "In-Suite Laundry", "Air Conditioning", "Heat Included"]);

        yield return (Make(landlordId,
            title: "Roncesvalles Family Flat",
            type: PropertyType.Apartment,
            street: "188 Roncesvalles Avenue",
            city: "Toronto", province: "ON", postal: "M6R 2L5",
            neighbourhood: "Roncesvalles",
            lat: 43.6464, lng: -79.4510,
            slug: "roncesvalles-family-flat",
            tier: ListingTier.Limited,
            description: "Pet-friendly second-floor flat with a huge backyard deck. Walk to Sorauren Park, the TTC, and coffee shops.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 1, SqFt = 880, Price = 2600, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)) } ],
            imageSeeds: ["ron-1", "ron-2"]),
            ["Pet Friendly", "Dogs Allowed", "Cats Allowed", "Hardwood Floors", "Storage Locker"]);

        yield return (Make(landlordId,
            title: "False Creek Waterfront",
            type: PropertyType.Condo,
            street: "1777 Bayshore Drive",
            city: "Vancouver", province: "BC", postal: "V6G 3H4",
            neighbourhood: "Coal Harbour",
            lat: 49.2930, lng: -123.1323,
            slug: "false-creek-waterfront",
            tier: ListingTier.Featured,
            description: "High-floor two-bedroom with wrap-around views, spa-inspired bathroom, and full concierge service.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 1120, Price = 5200, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) } ],
            imageSeeds: ["ch-1", "ch-2", "ch-3", "ch-4"]),
            ["Concierge", "Pool", "Sauna", "Gym", "Elevator", "24/7 Security", "Underground Parking", "In-Suite Laundry"]);
    }

    private static Property Make(
        Guid landlordId,
        string title,
        PropertyType type,
        string street,
        string city,
        string province,
        string postal,
        string? neighbourhood,
        double lat,
        double lng,
        string slug,
        ListingTier tier,
        string description,
        Unit[] units,
        string[] imageSeeds)
    {
        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordProfileId = landlordId,
            Title = title,
            Description = description,
            PropertyType = type,
            Status = ListingStatus.Active,
            Tier = tier,
            StreetAddress = street,
            City = city,
            Province = province,
            PostalCode = postal,
            Neighbourhood = neighbourhood,
            Latitude = lat,
            Longitude = lng,
            Slug = slug,
            IsVerified = true,
            PetsAllowed = imageSeeds.Length > 2,
            Furnished = false,
            LeaseTerm = Domain.LeaseTerm.OneYear
        };

        foreach (var u in units)
        {
            u.Id = Guid.NewGuid();
            u.PropertyId = property.Id;
            property.Units.Add(u);
        }

        for (var i = 0; i < imageSeeds.Length; i++)
        {
            property.Images.Add(new PropertyImage
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Url = $"https://picsum.photos/seed/{imageSeeds[i]}/1200/800",
                AltText = $"{title} - photo {i + 1}",
                IsPrimary = i == 0,
                DisplayOrder = i,
                Category = i == 0 ? "exterior" : "interior"
            });
        }

        return property;
    }
}
