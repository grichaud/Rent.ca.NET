using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.LandlordManage.Pages.Listings;

[Authorize(Roles = Roles.Landlord)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public IReadOnlyList<ListingRow> Listings { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        Listings = await _db.Properties
            .AsNoTracking()
            .Where(p => p.LandlordProfileId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ListingRow
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                CityName = p.City,
                Province = p.Province,
                Status = p.Status,
                Bedrooms = p.Units.OrderBy(u => u.Bedrooms).Select(u => u.Bedrooms).FirstOrDefault(),
                Price = p.Units.OrderBy(u => u.Price).Select(u => u.Price).FirstOrDefault(),
                ViewCount = p.ViewCount,
                LeadCount = p.LeadCount,
                PrimaryImageUrl = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault(),
                CitySlug = _db.Cities.Where(c => c.Name == p.City).Select(c => c.Slug).FirstOrDefault() ?? ""
            })
            .ToListAsync(ct);
    }

    public class ListingRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string CitySlug { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public ListingStatus Status { get; set; }
        public int Bedrooms { get; set; }
        public decimal Price { get; set; }
        public int ViewCount { get; set; }
        public int LeadCount { get; set; }
        public string? PrimaryImageUrl { get; set; }
    }
}
