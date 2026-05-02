using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.LandlordManage.Pages;

[Authorize(Roles = Roles.Landlord)]
public class InboxModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InboxModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string Filter { get; set; } = "all";

    public IReadOnlyList<InquiryRow> Inquiries { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int UnreadCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        var baseQuery = _db.ContactInquiries
            .AsNoTracking()
            .Where(i => i.Property.LandlordProfileId == userId);

        TotalCount = await baseQuery.CountAsync(ct);
        UnreadCount = await baseQuery.CountAsync(i => !i.IsRead, ct);

        var filtered = Filter == "unread" ? baseQuery.Where(i => !i.IsRead) : baseQuery;

        Inquiries = await filtered
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InquiryRow
            {
                Id = i.Id,
                PropertyTitle = i.Property.Title,
                PropertySlug = i.Property.Slug,
                CitySlug = _db.Cities.Where(c => c.Name == i.Property.City).Select(c => c.Slug).FirstOrDefault() ?? "",
                SenderName = i.SenderName,
                SenderEmail = i.SenderEmail,
                SenderPhone = i.SenderPhone,
                Message = i.Message,
                MoveInDate = i.MoveInDate,
                IsRead = i.IsRead,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid inquiryId, string? filter, CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var inquiry = await _db.ContactInquiries
            .Include(i => i.Property)
            .FirstOrDefaultAsync(i => i.Id == inquiryId && i.Property.LandlordProfileId == userId, ct);

        if (inquiry is not null)
        {
            inquiry.IsRead = !inquiry.IsRead;
            await _db.SaveChangesAsync(ct);
            TempData["InquiryAction"] = inquiry.IsRead ? "Marked as read." : "Marked as unread.";
        }

        var q = string.IsNullOrEmpty(filter) ? "" : $"?filter={filter}";
        return Redirect(this.Localized("/landlord/inbox") + q);
    }

    public class InquiryRow
    {
        public Guid Id { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public string PropertySlug { get; set; } = string.Empty;
        public string CitySlug { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string? SenderPhone { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateOnly? MoveInDate { get; set; }
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
