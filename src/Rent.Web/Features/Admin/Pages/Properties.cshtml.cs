using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Admin.Validators;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Admin.Pages;

[Authorize(Roles = Roles.Admin)]
public class PropertiesModel : PageModel
{
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    private readonly IValidator<SetTierRequest> _tierValidator;

    public PropertiesModel(AppDbContext db, IValidator<SetTierRequest> tierValidator)
    {
        _db = db;
        _tierValidator = tierValidator;
    }

    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    [BindProperty(SupportsGet = true)] public ListingTier? Tier { get; set; }
    [BindProperty(SupportsGet = true)] public ListingStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Landlord { get; set; }
    [BindProperty(SupportsGet = true, Name = "Page")] public int PageIndex { get; set; } = 1;

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalRows / PageSize);
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        FlashSuccess = TempData["AdminSuccess"] as string;
        FlashError = TempData["AdminError"] as string;

        var q = _db.Properties.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(City))
        {
            var c = City.Trim();
            q = q.Where(p => p.City.Contains(c));
        }
        if (Tier.HasValue) q = q.Where(p => p.Tier == Tier.Value);
        if (Status.HasValue) q = q.Where(p => p.Status == Status.Value);
        if (!string.IsNullOrWhiteSpace(Landlord))
        {
            var l = Landlord.Trim();
            q = q.Where(p => p.LandlordProfile.User.Email!.Contains(l));
        }

        TotalRows = await q.CountAsync(ct);
        if (PageIndex < 1) PageIndex = 1;

        // Project + sort in memory: SQLite (test fixture) can't ORDER BY DateTimeOffset.
        var raw = await q
            .Select(p => new Row
            {
                Id = p.Id,
                Title = p.Title,
                CityName = p.City,
                LandlordEmail = p.LandlordProfile.User.Email ?? "",
                Status = p.Status,
                Tier = p.Tier,
                TierExpiresAt = p.TierExpiresAt,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        Rows = raw
            .OrderByDescending(r => r.Tier)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnPostSetTierAsync(CancellationToken ct)
    {
        var req = new SetTierRequest
        {
            TargetId = Guid.TryParse(Request.Form["TargetId"], out var pid) ? pid : Guid.Empty,
            Tier = Enum.TryParse<ListingTier>(Request.Form["Tier"], out var t) ? t : ListingTier.Limited,
            ExpiresAt = TryParseExpires(Request.Form["ExpiresAt"])
        };

        var validation = await _tierValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            TempData["AdminError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return Redirect(BuildBackUrl());
        }

        var prop = await _db.Properties.FirstOrDefaultAsync(p => p.Id == req.TargetId, ct);
        if (prop is null)
        {
            TempData["AdminError"] = "Property not found.";
            return Redirect(BuildBackUrl());
        }

        prop.Tier = req.Tier;
        prop.TierExpiresAt = req.Tier == ListingTier.Limited ? null : req.ExpiresAt;
        prop.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = $"Tier updated for {prop.Title}.";
        return Redirect(BuildBackUrl());
    }

    private static DateTimeOffset? TryParseExpires(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTimeOffset.TryParse(s, out var dt) ? dt : null;
    }

    private string BuildBackUrl()
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(City)) qs.Add($"City={Uri.EscapeDataString(City)}");
        if (Tier.HasValue) qs.Add($"Tier={Tier.Value}");
        if (Status.HasValue) qs.Add($"Status={Status.Value}");
        if (!string.IsNullOrWhiteSpace(Landlord)) qs.Add($"Landlord={Uri.EscapeDataString(Landlord)}");
        if (PageIndex > 1) qs.Add($"Page={PageIndex}");

        var query = qs.Count == 0 ? string.Empty : "?" + string.Join("&", qs);
        return this.Localized("/admin/properties") + query;
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string LandlordEmail { get; set; } = string.Empty;
        public ListingStatus Status { get; set; }
        public ListingTier Tier { get; set; }
        public DateTimeOffset? TierExpiresAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public ListingTier EffectiveTier => ListingTierExtensions.Resolve(Tier, TierExpiresAt);
    }
}
