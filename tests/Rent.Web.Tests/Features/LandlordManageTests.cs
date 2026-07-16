using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Tests.Fixtures;

namespace Rent.Web.Tests.Features;

public class LandlordManageTests : IClassFixture<RentAppFactory>
{
    private readonly RentAppFactory _factory;

    public LandlordManageTests(RentAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Crea un landlord con su LandlordProfile (la FK que exige Property.LandlordProfileId).</summary>
    private async Task<ApplicationUser> CreateLandlordAsync(string emailPrefix, string fullName = "Landlord Tester")
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@test.local";
        var user = await TestAuth.CreateUserAsync(_factory, email, Roles.Landlord, fullName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.LandlordProfiles.AnyAsync(p => p.Id == user.Id))
        {
            db.LandlordProfiles.Add(new LandlordProfile { Id = user.Id, Tier = ListingTier.Limited });
            await db.SaveChangesAsync();
        }
        return user;
    }

    private async Task<Property> CreatePropertyAsync(Guid landlordId, string title, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordProfileId = landlordId,
            Title = title,
            Slug = slug,
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
            StreetAddress = "1 Test St",
            City = "Toronto",
            Province = "ON",
            PostalCode = "M5V 1A1"
        };
        property.Units.Add(new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Bedrooms = 2,
            Bathrooms = 1m,
            Price = 2000m,
            AvailableUnits = 1
        });
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    // El POST del toggle iba a "/landlord/inbox?handler=Toggle" (sin cultura). El middleware de
    // locale solo redirige GET/HEAD, asi que el POST caia en routing sin match -> 404.
    [Fact]
    public async Task Inbox_ToggleRead_PostWithCulture_DoesNotReturnNotFound()
    {
        var landlord = await CreateLandlordAsync("landlord-inbox");
        var property = await CreatePropertyAsync(landlord.Id, "Inbox Toggle Property", $"inbox-toggle-{Guid.NewGuid():N}");

        Guid inquiryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inquiry = new ContactInquiry
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                SenderName = "Prospect",
                SenderEmail = "prospect@test.local",
                Message = "Is this still available?",
                IsRead = false
            };
            db.ContactInquiries.Add(inquiry);
            await db.SaveChangesAsync();
            inquiryId = inquiry.Id;
        }

        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);
        var resp = await client.PostAsync("/en/landlord/inbox?handler=Toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("inquiryId", inquiryId.ToString()),
                new KeyValuePair<string, string>("filter", "all")
            }));

        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await vdb.ContactInquiries.AsNoTracking().FirstAsync(i => i.Id == inquiryId);
        updated.IsRead.Should().BeTrue();
    }

    // Reproduce la via REAL del usuario: el form renderizado posteaba a una URL sin cultura.
    [Fact]
    public async Task Inbox_ToggleForm_DoesNotTargetCultureLessUrl()
    {
        var landlord = await CreateLandlordAsync("landlord-form-action");
        var property = await CreatePropertyAsync(landlord.Id, "Form Action Property", $"form-action-{Guid.NewGuid():N}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ContactInquiries.Add(new ContactInquiry
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                SenderName = "Prospect",
                SenderEmail = "prospect2@test.local",
                Message = "Hello",
                IsRead = false
            });
            await db.SaveChangesAsync();
        }

        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);
        var resp = await client.GetAsync("/en/landlord/inbox");
        var body = await resp.Content.ReadAsStringAsync();

        // Sanity: la consulta tiene que estar en la pagina, si no la asercion de abajo pasa en vacio.
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Prospect");

        // Un action sin prefijo de cultura no matchea "/{culture:locale}/landlord/inbox" y el
        // middleware de locale solo redirige GET -> el POST daria 404.
        body.Should().NotContain("action=\"/landlord/inbox");
    }

    // Confirma la consecuencia: esa URL sin cultura efectivamente no existe para un POST.
    [Fact]
    public async Task Inbox_TogglePostToCultureLessUrl_Is404_WhichIsWhyTheFormMustNotUseIt()
    {
        var landlord = await CreateLandlordAsync("landlord-cultureless");
        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);

        var resp = await client.PostAsync("/landlord/inbox?handler=Toggle",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("inquiryId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("filter", "all")
            }));

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_OtherLandlordsListing_IsNotExposed()
    {
        var owner = await CreateLandlordAsync("landlord-owner");
        var property = await CreatePropertyAsync(owner.Id, "Secret Penthouse Of The Owner", $"secret-{Guid.NewGuid():N}");

        var intruder = await CreateLandlordAsync("landlord-intruder");
        using var client = await TestAuth.SignInAsync(_factory, intruder.Email!, TestAuth.DefaultPassword);

        var resp = await client.GetAsync($"/en/landlord/listings/edit/{property.Id}");
        var body = await resp.Content.ReadAsStringAsync();

        body.Should().NotContain("Secret Penthouse Of The Owner");
    }

    [Fact]
    public async Task Delete_OtherLandlordsListing_DoesNotDeactivateIt()
    {
        var owner = await CreateLandlordAsync("landlord-owner-del");
        var property = await CreatePropertyAsync(owner.Id, "Not Yours To Delete", $"notyours-{Guid.NewGuid():N}");

        var intruder = await CreateLandlordAsync("landlord-intruder-del");
        using var client = await TestAuth.SignInAsync(_factory, intruder.Email!, TestAuth.DefaultPassword);

        await client.PostAsync($"/en/landlord/listings/delete/{property.Id}", new FormUrlEncodedContent([]));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await db.Properties.AsNoTracking().FirstAsync(p => p.Id == property.Id);
        after.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Create_AssignsListingToTheSignedInLandlord()
    {
        var landlord = await CreateLandlordAsync("landlord-create");
        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);

        var title = $"Created By Owner {Guid.NewGuid():N}";
        var resp = await client.PostAsync("/en/landlord/listings/create",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.Title", title),
                new KeyValuePair<string, string>("Input.PropertyType", PropertyType.Apartment.ToString()),
                new KeyValuePair<string, string>("Input.StreetAddress", "10 Queen St W"),
                new KeyValuePair<string, string>("Input.CityName", "Toronto"),
                new KeyValuePair<string, string>("Input.Province", "ON"),
                new KeyValuePair<string, string>("Input.PostalCode", "M5H 2N2"),
                new KeyValuePair<string, string>("Input.Units[0].Bedrooms", "2"),
                new KeyValuePair<string, string>("Input.Units[0].Bathrooms", "1"),
                new KeyValuePair<string, string>("Input.Units[0].Price", "2100"),
                new KeyValuePair<string, string>("Input.Units[0].AvailableUnits", "1")
            }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Title == title);
        created.Should().NotBeNull();
        created!.LandlordProfileId.Should().Be(landlord.Id);
    }

    // Antes Edit solo exponia la unidad mas barata: las demas eran invisibles e ineditables
    // para su dueño, aunque el detalle publico si las mostraba a los inquilinos.
    [Fact]
    public async Task Edit_MultiUnitListing_ShowsEveryUnitAndKeepsTheirIds()
    {
        var landlord = await CreateLandlordAsync("landlord-multiunit");
        var property = await CreatePropertyAsync(landlord.Id, "Multi Unit Building", $"multiunit-{Guid.NewGuid():N}");

        Guid[] unitIds;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = await db.Properties.Include(x => x.Units).FirstAsync(x => x.Id == property.Id);
            // Add en el DbSet, no solo en la navegacion: Unit.Id ya viene con Guid.NewGuid() y EF
            // tomaria la unidad por existente (UPDATE de una fila que no existe).
            db.Units.Add(new Unit { Id = Guid.NewGuid(), PropertyId = p.Id, Bedrooms = 3, Bathrooms = 2m, Price = 3400m, AvailableUnits = 1 });
            db.Units.Add(new Unit { Id = Guid.NewGuid(), PropertyId = p.Id, Bedrooms = 1, Bathrooms = 1m, Price = 1600m, AvailableUnits = 1 });
            await db.SaveChangesAsync();
            unitIds = await db.Units.Where(u => u.PropertyId == p.Id).Select(u => u.Id).ToArrayAsync();
        }

        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);
        var body = await (await client.GetAsync($"/en/landlord/listings/edit/{property.Id}")).Content.ReadAsStringAsync();

        // Las 3 unidades se renderizan, cada una con su Id para que el POST haga merge y no recree.
        foreach (var id in unitIds)
            body.Should().Contain(id.ToString());
        body.Should().Contain("Input.Units[2].Price");
    }

    // Editar el titulo regeneraba el Slug -> la URL publica cambiaba sola y los links ya
    // enviados por email quedaban en 404. El slug se fija al crear y no se vuelve a tocar.
    [Fact]
    public async Task Edit_ChangingTitle_KeepsTheOriginalSlugResolvable()
    {
        var landlord = await CreateLandlordAsync("landlord-slug");
        var slug = $"stable-slug-{Guid.NewGuid():N}";
        var property = await CreatePropertyAsync(landlord.Id, "Original Title", slug);

        using var client = await TestAuth.SignInAsync(_factory, landlord.Email!, TestAuth.DefaultPassword);
        var resp = await client.PostAsync($"/en/landlord/listings/edit/{property.Id}",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Input.Title", "A Completely Different Title"),
                new KeyValuePair<string, string>("Input.PropertyType", PropertyType.Apartment.ToString()),
                new KeyValuePair<string, string>("Input.StreetAddress", "1 Test St"),
                new KeyValuePair<string, string>("Input.CityName", "Toronto"),
                new KeyValuePair<string, string>("Input.Province", "ON"),
                new KeyValuePair<string, string>("Input.PostalCode", "M5V 1A1"),
                new KeyValuePair<string, string>("Input.Units[0].Bedrooms", "2"),
                new KeyValuePair<string, string>("Input.Units[0].Bathrooms", "1"),
                new KeyValuePair<string, string>("Input.Units[0].Price", "2000"),
                new KeyValuePair<string, string>("Input.Units[0].AvailableUnits", "1")
            }));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await db.Properties.AsNoTracking().FirstAsync(p => p.Id == property.Id);

        after.Title.Should().Be("A Completely Different Title");
        after.Slug.Should().Be(slug, "la URL publica ya se compartio por email y en bookmarks");
    }
}
