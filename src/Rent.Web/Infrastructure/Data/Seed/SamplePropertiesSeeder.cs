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
        var landlordId = await EnsureDemoLandlordAsync(db, userManager);

        // Auto-upgrade prod databases that were seeded with the legacy Picsum image set.
        // Wipe the demo-landlord properties so the new sample list (with real-estate stock
        // photos and more cities) gets inserted on the next deploy. Property cascade handles
        // Units, Images, Inquiries, Favorites, and the PropertyAmenities junction.
        var hasLegacy = await db.Properties.AnyAsync(p =>
            p.LandlordProfileId == landlordId &&
            p.Images.Any(i => i.Url.Contains("picsum.photos")), ct);

        if (hasLegacy)
        {
            var legacy = await db.Properties
                .Where(p => p.LandlordProfileId == landlordId)
                .ToListAsync(ct);
            db.Properties.RemoveRange(legacy);
            await db.SaveChangesAsync(ct);
        }

        if (await db.Properties.AnyAsync(p => p.LandlordProfileId == landlordId, ct))
            return;

        var amenities = await db.Amenities.ToDictionaryAsync(a => a.Name, ct);
        foreach (var (property, amenityNames) in BuildSamples(landlordId))
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
            LogoUrl = Photo.Carousel[0]
        });

        return user.Id;
    }

    /// <summary>
    /// Real-estate stock photos lifted from the Next.js source (`new-listings-carousel.tsx`)
    /// + the city skylines from `cities.ts`. All verified-existing Unsplash assets so the
    /// portfolio demo never falls back to broken/random images.
    /// </summary>
    private static class Photo
    {
        private static string U(string id, int w = 1200, int h = 800) =>
            $"https://images.unsplash.com/photo-{id}?w={w}&h={h}&fit=crop&q=80";

        // Carousel photos: a mix of apartment exteriors, modern interiors, condos, and houses.
        public static readonly string[] Carousel =
        {
            U("1545324418-cc1a3fa10c00"),  // modern apartment exterior
            U("1486406146926-c627a92ad1ab"),  // bedroom (luxury condo)
            U("1564013799919-ab600027ffc6"),  // semi-detached house
            U("1502672260266-1c1ef2d93688"),  // modern living room (loft)
            U("1600596542815-ffad4c1539a9"),  // townhouse with backyard
            U("1600607687939-ce8a6c25118c"),  // executive condo interior
            U("1560518883-ce09059eeffa"),     // landlord hero (modern home)
            U("1517935706615-2717063c2225"),  // Toronto skyline
        };

        public static readonly string ApartmentExterior = Carousel[0];
        public static readonly string ApartmentInterior = Carousel[1];
        public static readonly string House           = Carousel[2];
        public static readonly string LoftInterior    = Carousel[3];
        public static readonly string TownhouseExt    = Carousel[4];
        public static readonly string CondoInterior   = Carousel[5];
        public static readonly string ModernHome      = Carousel[6];
    }

    private static IEnumerable<(Property property, string[] amenities)> BuildSamples(Guid landlordId)
    {
        // ---- Toronto (4) ----
        yield return (Make(landlordId,
            title: "Luxury Lofts on King Street",
            type: PropertyType.Loft,
            street: "250 King Street West", city: "Toronto", province: "ON", postal: "M5V 1J2",
            neighbourhood: "Entertainment District",
            lat: 43.6466, lng: -79.3901,
            slug: "luxury-lofts-on-king-street",
            tier: ListingTier.Featured,
            description: "Soaring 12-ft ceilings, exposed brick, and floor-to-ceiling windows overlooking King West. Walk to the CN Tower, restaurants, and TIFF.",
            units:
            [
                new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 620, Price = 2450, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)) },
                new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 950, Price = 3600, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) }
            ],
            images:
            [
                Photo.LoftInterior,
                Photo.ApartmentInterior,
                Photo.CondoInterior,
                Photo.ApartmentExterior,
            ]),
            ["Elevator", "Gym", "In-Suite Laundry", "Hardwood Floors", "Balcony", "Underground Parking", "24/7 Security", "Pet Friendly"]);

        yield return (Make(landlordId,
            title: "Bright 2BR Near High Park",
            type: PropertyType.Apartment,
            street: "1860 Bloor Street West", city: "Toronto", province: "ON", postal: "M6P 1P5",
            neighbourhood: "High Park",
            lat: 43.6544, lng: -79.4673,
            slug: "bright-2br-near-high-park",
            tier: ListingTier.Promoted,
            description: "Spacious two-bedroom with parquet floors, a sun-drenched living room, and a shared rooftop garden. Steps from the subway.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 1, SqFt = 780, Price = 2800, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)) } ],
            images: [ Photo.ApartmentInterior, Photo.ApartmentExterior, Photo.LoftInterior ]),
            ["Hardwood Floors", "In-Suite Laundry", "Cats Allowed", "Heat Included"]);

        yield return (Make(landlordId,
            title: "Roncesvalles Family Flat",
            type: PropertyType.Apartment,
            street: "188 Roncesvalles Avenue", city: "Toronto", province: "ON", postal: "M6R 2L5",
            neighbourhood: "Roncesvalles",
            lat: 43.6464, lng: -79.4510,
            slug: "roncesvalles-family-flat",
            tier: ListingTier.Limited,
            description: "Pet-friendly second-floor flat with a huge backyard deck. Walk to Sorauren Park, the TTC, and coffee shops.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 1, SqFt = 880, Price = 2600, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)) } ],
            images: [ Photo.House, Photo.ApartmentInterior, Photo.TownhouseExt ]),
            ["Pet Friendly", "Dogs Allowed", "Cats Allowed", "Hardwood Floors", "Storage Locker"]);

        yield return (Make(landlordId,
            title: "Yonge & Eglinton Modern Condo",
            type: PropertyType.Condo,
            street: "2221 Yonge Street", city: "Toronto", province: "ON", postal: "M4S 2B4",
            neighbourhood: "Yonge & Eglinton",
            lat: 43.7066, lng: -79.3982,
            slug: "yonge-eglinton-modern-condo",
            tier: ListingTier.Promoted,
            description: "Brand-new high-rise above the Eglinton Crosstown station. Floor-to-ceiling windows, granite counters, full amenities floor.",
            units:
            [
                new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 540, Price = 2350, AvailableUnits = 4, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) },
                new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 870, Price = 3300, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)) }
            ],
            images: [ Photo.CondoInterior, Photo.ApartmentExterior, Photo.ApartmentInterior ]),
            ["Gym", "Pool", "Concierge", "Elevator", "In-Suite Laundry", "Smart Access", "Underground Parking"]);

        // ---- Montreal (3) ----
        yield return (Make(landlordId,
            title: "Charming Plateau Triplex",
            type: PropertyType.Duplex,
            street: "4550 Rue Saint-Denis", city: "Montreal", province: "QC", postal: "H2J 2L4",
            neighbourhood: "Le Plateau",
            lat: 45.5258, lng: -73.5809,
            slug: "charming-plateau-triplex",
            tier: ListingTier.Promoted,
            description: "Classic Montreal triplex with original mouldings, wide plank floors, and a sun-filled kitchen. Steps from Mont-Royal Avenue.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 1.5m, SqFt = 1100, Price = 2300, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)) } ],
            images: [ Photo.House, Photo.TownhouseExt, Photo.ApartmentInterior ]),
            ["Hardwood Floors", "Balcony", "Pet Friendly", "Fireplace"]);

        yield return (Make(landlordId,
            title: "Old Montreal Heritage Loft",
            type: PropertyType.Loft,
            street: "445 Rue Saint-Pierre", city: "Montreal", province: "QC", postal: "H2Y 2M8",
            neighbourhood: "Vieux-Montréal",
            lat: 45.5026, lng: -73.5556,
            slug: "old-montreal-heritage-loft",
            tier: ListingTier.Featured,
            description: "Restored 19th-century warehouse with cast-iron columns, exposed brick, and a private terrace overlooking the cobblestones.",
            units: [ new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 950, Price = 2750, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)) } ],
            images: [ Photo.LoftInterior, Photo.CondoInterior, Photo.ApartmentExterior ]),
            ["Hardwood Floors", "Elevator", "Balcony", "In-Suite Laundry", "Air Conditioning"]);

        yield return (Make(landlordId,
            title: "Mile End Studio Apartment",
            type: PropertyType.Studio,
            street: "5145 Avenue du Parc", city: "Montreal", province: "QC", postal: "H2V 4G9",
            neighbourhood: "Mile End",
            lat: 45.5260, lng: -73.5970,
            slug: "mile-end-studio-apartment",
            tier: ListingTier.Limited,
            description: "Cozy studio in the heart of Mile End. Bagels, coffee, and the metro all within a 5-minute walk. Utilities included.",
            units: [ new Unit { Bedrooms = 0, Bathrooms = 1, SqFt = 380, Price = 1395, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) } ],
            images: [ Photo.ApartmentInterior, Photo.LoftInterior ]),
            ["Heat Included", "Water Included", "Hardwood Floors", "Cats Allowed"]);

        // ---- Vancouver (3) ----
        yield return (Make(landlordId,
            title: "Yaletown Skyline Condo",
            type: PropertyType.Condo,
            street: "1308 Hornby Street", city: "Vancouver", province: "BC", postal: "V6Z 0C4",
            neighbourhood: "Yaletown",
            lat: 49.2747, lng: -123.1207,
            slug: "yaletown-skyline-condo",
            tier: ListingTier.Featured,
            description: "Panoramic views of False Creek and the North Shore mountains. Modern finishes, in-suite laundry, and a rooftop pool.",
            units:
            [
                new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 580, Price = 2650, AvailableUnits = 3, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) },
                new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 880, Price = 3950, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)) }
            ],
            images: [ Photo.CondoInterior, Photo.ApartmentExterior, Photo.ApartmentInterior, Photo.LoftInterior ]),
            ["Gym", "Pool", "Concierge", "Elevator", "In-Suite Laundry", "Smart Access", "Underground Parking"]);

        yield return (Make(landlordId,
            title: "Kitsilano Beach House",
            type: PropertyType.House,
            street: "2245 Cornwall Avenue", city: "Vancouver", province: "BC", postal: "V6K 1B7",
            neighbourhood: "Kitsilano",
            lat: 49.2714, lng: -123.1558,
            slug: "kitsilano-beach-house",
            tier: ListingTier.Limited,
            description: "Detached 3-bedroom with a sun deck, garden, and a short walk to Kits Beach. Perfect for families.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 2, SqFt = 1650, Price = 4750, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)) } ],
            images: [ Photo.House, Photo.ModernHome ]),
            ["Fireplace", "Dogs Allowed", "Outdoor Parking"]);

        yield return (Make(landlordId,
            title: "Coal Harbour Waterfront",
            type: PropertyType.Condo,
            street: "1777 Bayshore Drive", city: "Vancouver", province: "BC", postal: "V6G 3H4",
            neighbourhood: "Coal Harbour",
            lat: 49.2930, lng: -123.1323,
            slug: "coal-harbour-waterfront",
            tier: ListingTier.Featured,
            description: "High-floor two-bedroom with wrap-around views, spa-inspired bathroom, and full concierge service.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 2, SqFt = 1120, Price = 5200, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) } ],
            images: [ Photo.CondoInterior, Photo.ApartmentExterior, Photo.LoftInterior, Photo.ApartmentInterior ]),
            ["Concierge", "Pool", "Sauna", "Gym", "Elevator", "24/7 Security", "Underground Parking", "In-Suite Laundry"]);

        // ---- Calgary (2) ----
        yield return (Make(landlordId,
            title: "Beltline Studio",
            type: PropertyType.Studio,
            street: "1011 12 Avenue SW", city: "Calgary", province: "AB", postal: "T2R 0J5",
            neighbourhood: "Beltline",
            lat: 51.0397, lng: -114.0791,
            slug: "beltline-studio",
            tier: ListingTier.Limited,
            description: "Cozy studio in the heart of the Beltline. Walking distance to 17th Ave shops and restaurants. Utilities included.",
            units: [ new Unit { Bedrooms = 0, Bathrooms = 1, SqFt = 420, Price = 1395, AvailableUnits = 4, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow) } ],
            images: [ Photo.ApartmentInterior, Photo.LoftInterior ]),
            ["Heat Included", "Water Included", "Elevator", "Gym"]);

        yield return (Make(landlordId,
            title: "Inglewood Loft Conversion",
            type: PropertyType.Loft,
            street: "1322 9 Avenue SE", city: "Calgary", province: "AB", postal: "T2G 0T5",
            neighbourhood: "Inglewood",
            lat: 51.0387, lng: -114.0379,
            slug: "inglewood-loft-conversion",
            tier: ListingTier.Promoted,
            description: "Two-storey loft in a converted warehouse. Polished concrete floors, mezzanine bedroom, and a private rooftop deck.",
            units: [ new Unit { Bedrooms = 1, Bathrooms = 1.5m, SqFt = 1050, Price = 2150, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)) } ],
            images: [ Photo.LoftInterior, Photo.CondoInterior, Photo.ApartmentExterior ]),
            ["Hardwood Floors", "Balcony", "Underground Parking", "Pet Friendly", "Air Conditioning"]);

        // ---- Ottawa (2) ----
        yield return (Make(landlordId,
            title: "ByWard Market Townhouse",
            type: PropertyType.Townhouse,
            street: "380 Dalhousie Street", city: "Ottawa", province: "ON", postal: "K1N 7E8",
            neighbourhood: "ByWard Market",
            lat: 45.4309, lng: -75.6927,
            slug: "byward-market-townhouse",
            tier: ListingTier.Promoted,
            description: "Three-level townhouse with private garage and rooftop patio. Steps from Parliament, restaurants, and the market.",
            units: [ new Unit { Bedrooms = 3, Bathrooms = 2.5m, SqFt = 1480, Price = 3200, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) } ],
            images: [ Photo.TownhouseExt, Photo.House, Photo.ModernHome ]),
            ["Fireplace", "Balcony", "EV Charging", "Air Conditioning", "In-Suite Laundry"]);

        yield return (Make(landlordId,
            title: "Glebe Heritage Apartment",
            type: PropertyType.Apartment,
            street: "788 Bank Street", city: "Ottawa", province: "ON", postal: "K1S 3V5",
            neighbourhood: "The Glebe",
            lat: 45.3949, lng: -75.6884,
            slug: "glebe-heritage-apartment",
            tier: ListingTier.Limited,
            description: "Top-floor unit in a 1920s heritage building. Original tin ceilings, refinished hardwood, and a quiet tree-lined street.",
            units: [ new Unit { Bedrooms = 2, Bathrooms = 1, SqFt = 740, Price = 1950, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)) } ],
            images: [ Photo.ApartmentInterior, Photo.ApartmentExterior, Photo.LoftInterior ]),
            ["Hardwood Floors", "Heat Included", "Cats Allowed"]);

        // ---- Edmonton (2) ----
        yield return (Make(landlordId,
            title: "Oliver Square One-Bedroom",
            type: PropertyType.Apartment,
            street: "10235 104 Street NW", city: "Edmonton", province: "AB", postal: "T5J 1B9",
            neighbourhood: "Oliver",
            lat: 53.5456, lng: -113.4936,
            slug: "oliver-square-one-bedroom",
            tier: ListingTier.Limited,
            description: "Modern one-bedroom in downtown Edmonton with a balcony, in-suite laundry, and access to a shared fitness room.",
            units: [ new Unit { Bedrooms = 1, Bathrooms = 1, SqFt = 540, Price = 1450, AvailableUnits = 2, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)) } ],
            images: [ Photo.ApartmentInterior, Photo.ApartmentExterior ]),
            ["Gym", "Elevator", "In-Suite Laundry", "Air Conditioning", "Heat Included"]);

        yield return (Make(landlordId,
            title: "Whyte Avenue Studio",
            type: PropertyType.Studio,
            street: "10350 82 Avenue NW", city: "Edmonton", province: "AB", postal: "T6E 1Z9",
            neighbourhood: "Old Strathcona",
            lat: 53.5188, lng: -113.4923,
            slug: "whyte-avenue-studio",
            tier: ListingTier.Promoted,
            description: "Bright studio above a bookstore on Whyte Ave. Steps from cafes, music venues, and the Saturday farmers market.",
            units: [ new Unit { Bedrooms = 0, Bathrooms = 1, SqFt = 360, Price = 1150, AvailableUnits = 1, AvailableDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) } ],
            images: [ Photo.LoftInterior, Photo.ApartmentInterior ]),
            ["Heat Included", "Water Included", "Hardwood Floors"]);
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
        string[] images)
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
            // Preserve a small bit of variety. Most apartments/houses are pet friendly in this demo.
            PetsAllowed = type != PropertyType.Studio,
            Furnished = false,
            LeaseTerm = Domain.LeaseTerm.OneYear
        };

        foreach (var u in units)
        {
            u.Id = Guid.NewGuid();
            u.PropertyId = property.Id;
            property.Units.Add(u);
        }

        for (var i = 0; i < images.Length; i++)
        {
            property.Images.Add(new PropertyImage
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Url = images[i],
                AltText = $"{title} - photo {i + 1}",
                IsPrimary = i == 0,
                DisplayOrder = i,
                Category = i == 0 ? "exterior" : "interior"
            });
        }

        return property;
    }
}
