using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.RenterPortal.Pages;

[Authorize(Roles = Roles.Renter)]
public class InquiriesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InquiriesModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public IReadOnlyList<InquiryRow> Inquiries { get; private set; } = Array.Empty<InquiryRow>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(_userManager.GetUserId(User), out var uid)) return;

        var raw = await _db.ContactInquiries
            .AsNoTracking()
            .Where(i => i.SenderUserId == uid)
            .Select(i => new
            {
                i.Id,
                i.CreatedAt,
                i.MoveInDate,
                i.Message,
                PropertyTitle = i.Property.Title,
                PropertySlug = i.Property.Slug,
                PropertyCity = i.Property.City,
                CitySlug = _db.Cities
                    .Where(c => c.Name == i.Property.City)
                    .Select(c => c.Slug)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(ct);

        Inquiries = raw
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new InquiryRow
            {
                Id = r.Id,
                PropertyTitle = r.PropertyTitle,
                PropertySlug = r.PropertySlug,
                PropertyCity = r.PropertyCity,
                CitySlug = r.CitySlug,
                Message = r.Message,
                MoveInDate = r.MoveInDate,
                CreatedAt = r.CreatedAt
            })
            .ToList();
    }

    public class InquiryRow
    {
        public Guid Id { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public string PropertySlug { get; set; } = string.Empty;
        public string PropertyCity { get; set; } = string.Empty;
        public string CitySlug { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateOnly? MoveInDate { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
