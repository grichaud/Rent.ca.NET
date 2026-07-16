using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.LandlordManage.Pages;

[Authorize(Roles = Roles.Landlord)]
public class DashboardModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public DashboardModel(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public string? FirstName { get; private set; }
    public int TotalListings { get; private set; }
    public int TotalViews { get; private set; }
    public int TotalInquiries { get; private set; }
    public int UnreadInquiries { get; private set; }
    public IReadOnlyList<RecentInquiry> Recent { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return;

        FirstName = string.IsNullOrWhiteSpace(user.FullName) ? null : user.FullName.Trim().Split(' ')[0];

        var listings = _db.Properties.AsNoTracking().Where(p => p.LandlordProfileId == user.Id);
        TotalListings = await listings.CountAsync(ct);
        TotalViews = await listings.SumAsync(p => p.ViewCount, ct);

        // Se cuentan filas de ContactInquiries, no Property.LeadCount: LeadCount es un contador
        // desnormalizado que solo sube, asi que discreparia del Inbox.
        var inquiries = _db.ContactInquiries.AsNoTracking().Where(i => i.Property.LandlordProfileId == user.Id);
        TotalInquiries = await inquiries.CountAsync(ct);
        UnreadInquiries = await inquiries.CountAsync(i => !i.IsRead, ct);

        // Over-fetch + sort in memory to avoid SQLite ORDER BY DateTimeOffset issue on tests.
        var raw = await inquiries
            .Select(i => new RecentInquiry
            {
                Id = i.Id,
                SenderName = i.SenderName,
                PropertyTitle = i.Property.Title,
                IsRead = i.IsRead,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(ct);

        Recent = raw.OrderByDescending(i => i.CreatedAt).Take(5).ToList();
    }

    public class RecentInquiry
    {
        public Guid Id { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string PropertyTitle { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
