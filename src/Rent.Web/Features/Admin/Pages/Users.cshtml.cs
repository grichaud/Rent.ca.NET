using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Admin.Pages;

[Authorize(Roles = Roles.Admin)]
public class UsersModel : PageModel
{
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)] public string? Email { get; set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        FlashSuccess = TempData["AdminSuccess"] as string;
        FlashError = TempData["AdminError"] as string;

        var q = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Email))
        {
            var f = Email.Trim();
            q = q.Where(u => u.Email != null && u.Email.Contains(f));
        }

        var rawUsers = await q
            .Select(u => new { u.Id, u.Email, u.FullName, u.CreatedAt })
            .ToListAsync(ct);

        var ordered = rawUsers
            .OrderByDescending(u => u.CreatedAt)
            .Take(PageSize)
            .ToList();

        // Pull all role mappings + role names; do the join in memory (small data set, simple).
        var ids = ordered.Select(u => u.Id).ToHashSet();
        var roleLinks = await _db.UserRoles
            .AsNoTracking()
            .Where(r => ids.Contains(r.UserId))
            .ToListAsync(ct);
        var roleMap = await _db.Roles
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? "", ct);

        Rows = ordered
            .Select(u => new Row
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FullName = u.FullName,
                CreatedAt = u.CreatedAt,
                Roles = roleLinks
                    .Where(r => r.UserId == u.Id)
                    .Select(r => roleMap.TryGetValue(r.RoleId, out var n) ? n : "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderBy(n => n)
                    .ToList()
            })
            .ToList();
    }

    public async Task<IActionResult> OnPostToggleAdminAsync(CancellationToken ct)
    {
        var userIdStr = Request.Form["UserId"].ToString();
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            TempData["AdminError"] = "Invalid user id.";
            return Redirect(this.Localized("/admin/users"));
        }

        var target = await _userManager.FindByIdAsync(userId.ToString());
        if (target is null)
        {
            TempData["AdminError"] = "User not found.";
            return Redirect(this.Localized("/admin/users"));
        }

        var currentlyAdmin = await _userManager.IsInRoleAsync(target, Roles.Admin);

        if (currentlyAdmin)
        {
            // Safety: never leave the system without an admin.
            var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
            {
                TempData["AdminError"] = "Cannot remove the last admin. Promote another user first.";
                return Redirect(this.Localized("/admin/users"));
            }

            var result = await _userManager.RemoveFromRoleAsync(target, Roles.Admin);
            TempData[result.Succeeded ? "AdminSuccess" : "AdminError"] = result.Succeeded
                ? $"Removed Admin role from {target.Email}."
                : "Failed to remove role: " + string.Join(", ", result.Errors.Select(e => e.Description));
        }
        else
        {
            var result = await _userManager.AddToRoleAsync(target, Roles.Admin);
            TempData[result.Succeeded ? "AdminSuccess" : "AdminError"] = result.Succeeded
                ? $"Granted Admin role to {target.Email}."
                : "Failed to add role: " + string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return Redirect(this.Localized("/admin/users") + (string.IsNullOrEmpty(Email) ? string.Empty : $"?Email={Uri.EscapeDataString(Email)}"));
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public bool IsAdmin => Roles.Contains(Rent.Web.Infrastructure.Identity.Roles.Admin);
    }
}
