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
public class LandlordsModel : PageModel
{
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    private readonly IValidator<SetTierRequest> _tierValidator;

    public LandlordsModel(AppDbContext db, IValidator<SetTierRequest> tierValidator)
    {
        _db = db;
        _tierValidator = tierValidator;
    }

    [BindProperty(SupportsGet = true)] public string? Email { get; set; }
    [BindProperty(SupportsGet = true)] public ListingTier? Tier { get; set; }
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

        var q = _db.LandlordProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(Email))
        {
            var e = Email.Trim();
            q = q.Where(l => l.User.Email!.Contains(e));
        }
        if (Tier.HasValue) q = q.Where(l => l.Tier == Tier.Value);

        TotalRows = await q.CountAsync(ct);
        if (PageIndex < 1) PageIndex = 1;

        Rows = await q
            .OrderByDescending(l => l.Tier)
            .ThenBy(l => l.User.Email)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .Select(l => new Row
            {
                Id = l.Id,
                Email = l.User.Email ?? "",
                CompanyName = l.CompanyName,
                Tier = l.Tier,
                TierExpiresAt = l.TierExpiresAt,
                IsVerified = l.IsVerified,
                ListingsCount = l.Properties.Count
            })
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostSetTierAsync(CancellationToken ct)
    {
        var req = new SetTierRequest
        {
            TargetId = Guid.TryParse(Request.Form["TargetId"], out var lid) ? lid : Guid.Empty,
            Tier = Enum.TryParse<ListingTier>(Request.Form["Tier"], out var t) ? t : ListingTier.Limited,
            ExpiresAt = TryParseExpires(Request.Form["ExpiresAt"])
        };

        var validation = await _tierValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            TempData["AdminError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return Redirect(BuildBackUrl());
        }

        var landlord = await _db.LandlordProfiles
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == req.TargetId, ct);
        if (landlord is null)
        {
            TempData["AdminError"] = "Landlord not found.";
            return Redirect(BuildBackUrl());
        }

        landlord.Tier = req.Tier;
        landlord.TierExpiresAt = req.Tier == ListingTier.Limited ? null : req.ExpiresAt;
        landlord.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = $"Tier updated for {landlord.User.Email}.";
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
        if (!string.IsNullOrWhiteSpace(Email)) qs.Add($"Email={Uri.EscapeDataString(Email)}");
        if (Tier.HasValue) qs.Add($"Tier={Tier.Value}");
        if (PageIndex > 1) qs.Add($"Page={PageIndex}");

        var query = qs.Count == 0 ? string.Empty : "?" + string.Join("&", qs);
        return this.Localized("/admin/landlords") + query;
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public ListingTier Tier { get; set; }
        public DateTimeOffset? TierExpiresAt { get; set; }
        public bool IsVerified { get; set; }
        public int ListingsCount { get; set; }
        public ListingTier EffectiveTier => ListingTierExtensions.Resolve(Tier, TierExpiresAt);
    }
}
