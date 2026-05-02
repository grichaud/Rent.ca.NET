using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Infrastructure.Data.Seed;

public static class SamplePropertiesSeeder
{
    private const string DemoLandlordEmail = "demo.landlord@rentca.net";
    private const string DemoLandlordPassword = "DemoLandlord1!";

    // Bump this when changing the sample data so prod re-seeds itself.
    // The version string is stored in the demo landlord's Description field. If it
    // does not match, all demo-landlord properties are wiped and re-inserted.
    private const string SeedVersion = "v3-2026-05-02-curated-unsplash";

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct = default)
    {
        var (landlordId, profile) = await EnsureDemoLandlordAsync(db, userManager);

        // Auto-upgrade prod databases whose seed predates this version. The signal is
        // the LandlordProfile.Description sentinel; on mismatch we wipe the demo-landlord
        // properties (Property cascade handles Units, Images, Inquiries, Favorites and the
        // PropertyAmenities junction) and re-insert the canonical catalog below.
        var seedTag = $"[seed:{SeedVersion}]";
        if (profile.Description is null || !profile.Description.Contains(seedTag))
        {
            var legacy = await db.Properties
                .Where(p => p.LandlordProfileId == landlordId)
                .ToListAsync(ct);
            if (legacy.Count > 0)
            {
                db.Properties.RemoveRange(legacy);
                await db.SaveChangesAsync(ct);
            }
            profile.Description = $"Sample listings populated by the seeder for portfolio demo purposes. {seedTag}";
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

    private static async Task<(Guid id, LandlordProfile profile)> EnsureDemoLandlordAsync(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        var existing = await userManager.FindByEmailAsync(DemoLandlordEmail);
        if (existing is not null)
        {
            var existingProfile = await db.LandlordProfiles.FirstAsync(p => p.Id == existing.Id);
            return (existing.Id, existingProfile);
        }

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

        var profile = new LandlordProfile
        {
            Id = user.Id,
            CompanyName = "Demo Properties Inc.",
            Description = "Sample listings populated by the seeder for portfolio demo purposes.",
            IsVerified = true,
            Tier = ListingTier.Featured,
            LogoUrl = Photo.ApartmentBuilding
        };
        db.LandlordProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (user.Id, profile);
    }

    /// <summary>
    /// Real-estate stock photos. Each ID was pulled from a Unsplash topic search
    /// (`modern-apartment-interior`, `condo-building`, `modern-house-exterior`, `loft-apartment`)
    /// and visually verified to be a residential property. Do NOT swap an ID without
    /// loading it in a browser first.
    /// </summary>
    private static class Photo
    {
        private static string U(string id, int w = 1200, int h = 800) =>
            $"https://images.unsplash.com/photo-{id}?w={w}&h={h}&fit=crop&q=80";

        // Apartment / condo interiors (living rooms, kitchens, dining areas).
        public static readonly string ApartmentInterior1 = U("1603072845032-7b5bd641a82a"); // modern living room with a yellow cabinet and city view
        public static readonly string ApartmentInterior2 = U("1738168279272-c08d6dd22002"); // living room with couch, table and chairs
        public static readonly string ApartmentInterior3 = U("1647082550285-119acfd169f2"); // living room with a large painting on the wall
        public static readonly string ApartmentInterior4 = U("1666282167632-c613fbeb163c"); // living room with a couch and a coffee table
        public static readonly string ApartmentInterior5 = U("1737233459465-8eaf6c7d8856"); // living room with furniture and dining table
        public static readonly string ApartmentInterior6 = U("1738168246881-40f35f8aba0a"); // living room with a large green couch

        // Apartment / condo building exteriors.
        public static readonly string ApartmentBuilding  = U("1573921470445-8d99c48c879f"); // high-rise condo under a blue sky (verified)
        public static readonly string ApartmentBuilding2 = U("1770962282626-61b2f4931bf7"); // modern apartment building, dark grey accents
        public static readonly string ApartmentBuilding3 = U("1773558061377-fd3fa0cc2447"); // modern apartment balconies under blue sky
        public static readonly string ApartmentBuilding4 = U("1766761562522-5a0a12bd2a27"); // tall apartment buildings with balconies

        // Detached / semi-detached / townhouse exteriors.
        public static readonly string House1 = U("1721815693498-cc28507c0ba2"); // modern 2-storey house with windows + balconies (verified)
        public static readonly string House2 = U("1706808849777-96e0d7be3bb7"); // modern house with a large front yard
        public static readonly string House3 = U("1706808849780-7a04fbac83ef"); // modern house with a pool and lounge chairs
        public static readonly string House4 = U("1513584684374-8bab748fbf90"); // landscape photo of a 2-storey house

        // Loft + bedroom for variety.
        public static readonly string LoftInterior = U("1505873242700-f289a29e1e0f"); // black leather couch with throw pillow (loft style)
        public static readonly string Bedroom      = U("1662454419716-c4c504728811"); // bed in a sunlit room
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
                Photo.ApartmentInterior4,
                Photo.ApartmentBuilding3,
                Photo.Bedroom,
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
            images: [ Photo.ApartmentInterior2, Photo.ApartmentBuilding2, Photo.Bedroom ]),
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
            images: [ Photo.House2, Photo.ApartmentInterior5, Photo.Bedroom ]),
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
            images: [ Photo.ApartmentBuilding, Photo.ApartmentInterior1, Photo.ApartmentInterior3 ]),
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
            images: [ Photo.House4, Photo.ApartmentInterior5, Photo.Bedroom ]),
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
            images: [ Photo.LoftInterior, Photo.ApartmentInterior6, Photo.Bedroom ]),
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
            images: [ Photo.ApartmentInterior4, Photo.Bedroom ]),
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
            images: [ Photo.ApartmentBuilding, Photo.ApartmentInterior1, Photo.ApartmentInterior3, Photo.Bedroom ]),
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
            images: [ Photo.House3, Photo.House2, Photo.ApartmentInterior2 ]),
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
            images: [ Photo.ApartmentBuilding4, Photo.ApartmentInterior1, Photo.ApartmentInterior6, Photo.Bedroom ]),
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
            images: [ Photo.ApartmentInterior3, Photo.Bedroom ]),
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
            images: [ Photo.LoftInterior, Photo.ApartmentInterior6, Photo.ApartmentBuilding3 ]),
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
            images: [ Photo.House2, Photo.House4, Photo.ApartmentInterior5 ]),
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
            images: [ Photo.ApartmentInterior3, Photo.ApartmentBuilding2, Photo.Bedroom ]),
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
            images: [ Photo.ApartmentInterior2, Photo.ApartmentBuilding ]),
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
            images: [ Photo.ApartmentInterior6, Photo.Bedroom ]),
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
