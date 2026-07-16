using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.RenterPortal.Pages;

[Authorize(Roles = Roles.Renter)]
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
    public int SavedProperties { get; private set; }
    public int ActiveAlerts { get; private set; }
    public int InquiriesSent { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return;

        FirstName = string.IsNullOrWhiteSpace(user.FullName) ? null : user.FullName.Trim().Split(' ')[0];

        SavedProperties = await _db.Favorites.CountAsync(f => f.UserId == user.Id, ct);
        ActiveAlerts = await _db.Alerts.CountAsync(a => a.UserId == user.Id && a.IsActive, ct);
        InquiriesSent = await _db.ContactInquiries.CountAsync(i => i.SenderUserId == user.Id, ct);
    }
}
