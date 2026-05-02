using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Alerts.Pages;

[Authorize(Roles = Roles.Renter)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<CreateAlertRequest> _validator;

    public IndexModel(AppDbContext db, UserManager<ApplicationUser> userManager, IValidator<CreateAlertRequest> validator)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
    }

    [BindProperty]
    public CreateAlertRequest Input { get; set; } = new();

    public IReadOnlyList<Alert> Alerts { get; private set; } = Array.Empty<Alert>();
    public IReadOnlyList<City> Cities { get; private set; } = Array.Empty<City>();
    public bool ShowForm { get; private set; }
    public string? FlashSuccess { get; private set; }

    public async Task OnGetAsync([FromQuery] bool showForm, CancellationToken ct)
    {
        ShowForm = showForm;
        FlashSuccess = TempData["AlertSuccess"] as string;
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var userId = GetUserIdOrNull();
        if (userId is null) return Forbid();

        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName.Replace("Input.", "")}", err.ErrorMessage);
            ShowForm = true;
            await LoadAsync(ct);
            return Page();
        }

        var pets = Input.PetsAllowed switch
        {
            "true" => (bool?)true,
            "false" => (bool?)false,
            _ => null
        };

        _db.Alerts.Add(new Alert
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            City = string.IsNullOrWhiteSpace(Input.City) ? null : Input.City.Trim(),
            PropertyType = Input.PropertyType,
            PriceMin = Input.PriceMin,
            PriceMax = Input.PriceMax,
            BedroomsMin = Input.BedroomsMin,
            PetsAllowed = pets,
            Frequency = Input.Frequency,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        TempData["AlertSuccess"] = "Alert created.";
        return Redirect(this.Localized("/renter/alerts"));
    }

    public async Task<IActionResult> OnPostToggleAsync([FromForm] Guid id, CancellationToken ct)
    {
        var userId = GetUserIdOrNull();
        if (userId is null) return Forbid();

        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert is null) return NotFound();

        alert.IsActive = !alert.IsActive;
        alert.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["AlertSuccess"] = alert.IsActive ? "Alert resumed." : "Alert paused.";
        return Redirect(this.Localized("/renter/alerts"));
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        var userId = GetUserIdOrNull();
        if (userId is null) return Forbid();

        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert is null) return NotFound();

        _db.Alerts.Remove(alert);
        await _db.SaveChangesAsync(ct);

        TempData["AlertSuccess"] = "Alert deleted.";
        return Redirect(this.Localized("/renter/alerts"));
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var userId = GetUserIdOrNull();
        if (userId is null) return;

        // Over-fetch + sort in memory to avoid SQLite ORDER BY DateTimeOffset issue on tests.
        var raw = await _db.Alerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        Alerts = raw.OrderByDescending(a => a.CreatedAt).ToList();
        Cities = await _db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
    }

    private Guid? GetUserIdOrNull()
        => Guid.TryParse(_userManager.GetUserId(User), out var uid) ? uid : null;
}
