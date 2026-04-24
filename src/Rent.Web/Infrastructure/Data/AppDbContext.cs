using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;

namespace Rent.Web.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<LandlordProfile> LandlordProfiles => Set<LandlordProfile>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.AvatarUrl).HasMaxLength(500);
        });

        builder.Entity<LandlordProfile>(e =>
        {
            e.ToTable("LandlordProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.CompanyName).HasMaxLength(200);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.Website).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.User)
             .WithOne(x => x.LandlordProfile)
             .HasForeignKey<LandlordProfile>(x => x.Id)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<City>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Province).IsRequired().HasMaxLength(50);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(100);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.IsFeatured);
        });

        builder.Entity<Property>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.StreetAddress).IsRequired().HasMaxLength(300);
            e.Property(x => x.City).IsRequired().HasMaxLength(100);
            e.Property(x => x.Province).IsRequired().HasMaxLength(50);
            e.Property(x => x.PostalCode).IsRequired().HasMaxLength(10);
            e.Property(x => x.Neighbourhood).HasMaxLength(100);
            e.Property(x => x.ParkingType).HasMaxLength(50);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(200);

            e.Property(x => x.PropertyType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LeaseTerm).HasConversion<string>().HasMaxLength(20);

            e.HasIndex(x => new { x.City, x.Status });
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Status, x.Tier });

            e.HasOne(x => x.LandlordProfile)
             .WithMany(x => x.Properties)
             .HasForeignKey(x => x.LandlordProfileId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Unit>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Bathrooms).HasPrecision(3, 1);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.PriceMax).HasPrecision(10, 2);

            e.HasOne(x => x.Property)
             .WithMany(x => x.Units)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PropertyImage>(e =>
        {
            e.Property(x => x.Url).IsRequired().HasMaxLength(1000);
            e.Property(x => x.AltText).HasMaxLength(300);
            e.Property(x => x.Category).HasMaxLength(50);

            e.HasOne(x => x.Property)
             .WithMany(x => x.Images)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Amenity>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Icon).HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();

            e.HasMany(x => x.Properties)
             .WithMany(x => x.Amenities)
             .UsingEntity(j => j.ToTable("PropertyAmenities"));
        });

        builder.Entity<ContactInquiry>(e =>
        {
            e.Property(x => x.SenderName).IsRequired().HasMaxLength(200);
            e.Property(x => x.SenderEmail).IsRequired().HasMaxLength(256);
            e.Property(x => x.SenderPhone).HasMaxLength(30);
            e.Property(x => x.Message).IsRequired().HasMaxLength(4000);

            e.HasIndex(x => new { x.PropertyId, x.IsRead });

            e.HasOne(x => x.Property)
             .WithMany(x => x.Inquiries)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.SenderUser)
             .WithMany()
             .HasForeignKey(x => x.SenderUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
