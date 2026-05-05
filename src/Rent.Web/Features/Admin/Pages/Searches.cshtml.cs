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
public class SearchesModel : PageModel
{
    private const int Top = 20;

    private readonly AppDbContext _db;
    private readonly IValidator<UpdatePopularSearchRequest> _updateValidator;

    public SearchesModel(AppDbContext db, IValidator<UpdatePopularSearchRequest> updateValidator)
    {
        _db = db;
        _updateValidator = updateValidator;
    }

    [BindProperty(SupportsGet = true, Name = "q")] public string? FilterQuery { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EditId { get; set; }

    public IReadOnlyList<PopularSearch> Rows { get; private set; } = [];
    public PopularSearch? EditingRow { get; private set; }
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        FlashSuccess = TempData["AdminSuccess"] as string;
        FlashError = TempData["AdminError"] as string;

        var q = _db.PopularSearches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(FilterQuery))
        {
            var f = FilterQuery.Trim();
            q = q.Where(s => s.NormalizedQuery.Contains(f) || s.CitySlug.Contains(f));
        }

        // Sort/take in memory: SQLite (test fixture) can't ORDER BY DateTimeOffset.
        var raw = await q.ToListAsync(ct);
        Rows = raw
            .OrderByDescending(r => r.SearchCount)
            .ThenByDescending(r => r.LastSearchedAt)
            .Take(Top)
            .ToList();

        if (EditId.HasValue)
        {
            EditingRow = await _db.PopularSearches
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == EditId.Value, ct);
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken ct)
    {
        var req = new UpdatePopularSearchRequest
        {
            Id = ParseGuid(Request.Form["Id"]),
            NormalizedQuery = Request.Form["NormalizedQuery"].ToString(),
            CitySlug = Request.Form["CitySlug"].ToString()
        };

        var validation = await _updateValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            TempData["AdminError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return Redirect(this.Localized("/admin/searches") + $"?editId={req.Id}");
        }

        var entry = await _db.PopularSearches.FirstOrDefaultAsync(s => s.Id == req.Id, ct);
        if (entry is null)
        {
            TempData["AdminError"] = "Entry not found.";
            return Redirect(this.Localized("/admin/searches"));
        }

        entry.NormalizedQuery = req.NormalizedQuery.Trim().ToLowerInvariant();
        entry.CitySlug = (req.CitySlug ?? string.Empty).Trim().ToLowerInvariant();

        try
        {
            await _db.SaveChangesAsync(ct);
            TempData["AdminSuccess"] = "Search entry updated.";
        }
        catch (DbUpdateException)
        {
            TempData["AdminError"] = "An entry with that query/city already exists. Use Delete instead.";
        }

        return Redirect(this.Localized("/admin/searches"));
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken ct)
    {
        var id = ParseGuid(Request.Form["Id"]);
        var entry = await _db.PopularSearches.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entry is null)
        {
            TempData["AdminError"] = "Entry not found.";
            return Redirect(this.Localized("/admin/searches"));
        }

        _db.PopularSearches.Remove(entry);
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = "Search entry deleted.";
        return Redirect(this.Localized("/admin/searches"));
    }

    private static Guid ParseGuid(Microsoft.Extensions.Primitives.StringValues raw)
        => Guid.TryParse(raw.ToString(), out var g) ? g : Guid.Empty;
}
