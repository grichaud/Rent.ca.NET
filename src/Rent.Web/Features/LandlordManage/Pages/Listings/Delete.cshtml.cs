using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.LandlordManage.Pages.Listings;

[Authorize(Roles = Roles.Landlord)]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DeleteModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public bool ListingMissing { get; private set; }
    public string Title { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var prop = await _db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);
        if (prop is null) { ListingMissing = true; return Page(); }
        Title = prop.Title;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var prop = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == Id && p.LandlordProfileId == userId, ct);
        if (prop is null) { ListingMissing = true; return Page(); }

        prop.Status = ListingStatus.Inactive;
        prop.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["ListingSuccess"] = $"\"{prop.Title}\" has been deactivated.";
        return Redirect("/landlord/listings");
    }
}
