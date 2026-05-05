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
public class SpecialsModel : PageModel
{
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    private readonly IValidator<CreateRentSpecialRequest> _createValidator;
    private readonly IValidator<UpdateRentSpecialRequest> _updateValidator;

    public SpecialsModel(
        AppDbContext db,
        IValidator<CreateRentSpecialRequest> createValidator,
        IValidator<UpdateRentSpecialRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [BindProperty(SupportsGet = true, Name = "Page")] public int PageIndex { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public Guid? EditId { get; set; }
    [BindProperty(SupportsGet = true)] public bool ShowForm { get; set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalRows / PageSize);
    public IReadOnlyList<PropertyOption> PropertyOptions { get; private set; } = [];
    public Row? EditingRow { get; private set; }
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        FlashSuccess = TempData["AdminSuccess"] as string;
        FlashError = TempData["AdminError"] as string;

        await LoadAsync(ct);

        if (EditId.HasValue)
        {
            EditingRow = Rows.FirstOrDefault(r => r.Id == EditId.Value)
                ?? await LoadEditingRowAsync(EditId.Value, ct);
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var req = new CreateRentSpecialRequest
        {
            PropertyId = ParseGuid(Request.Form["PropertyId"]),
            Title = Request.Form["Title"].ToString(),
            Description = NullIfEmpty(Request.Form["Description"]),
            StartDate = ParseDate(Request.Form["StartDate"]),
            EndDate = ParseDate(Request.Form["EndDate"]),
            IsActive = ParseBool(Request.Form["IsActive"], defaultValue: true)
        };

        var validation = await _createValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            TempData["AdminError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return Redirect(this.Localized("/admin/specials") + "?showForm=true");
        }

        var propertyExists = await _db.Properties.AnyAsync(p => p.Id == req.PropertyId, ct);
        if (!propertyExists)
        {
            TempData["AdminError"] = "Property not found.";
            return Redirect(this.Localized("/admin/specials") + "?showForm=true");
        }

        _db.RentSpecials.Add(new RentSpecial
        {
            Id = Guid.NewGuid(),
            PropertyId = req.PropertyId,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            IsActive = req.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = $"Special '{req.Title}' created.";
        return Redirect(this.Localized("/admin/specials"));
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken ct)
    {
        var req = new UpdateRentSpecialRequest
        {
            Id = ParseGuid(Request.Form["Id"]),
            Title = Request.Form["Title"].ToString(),
            Description = NullIfEmpty(Request.Form["Description"]),
            StartDate = ParseDate(Request.Form["StartDate"]),
            EndDate = ParseDate(Request.Form["EndDate"]),
            IsActive = ParseBool(Request.Form["IsActive"], defaultValue: true)
        };

        var validation = await _updateValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
        {
            TempData["AdminError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return Redirect(this.Localized("/admin/specials") + $"?editId={req.Id}");
        }

        var special = await _db.RentSpecials.FirstOrDefaultAsync(s => s.Id == req.Id, ct);
        if (special is null)
        {
            TempData["AdminError"] = "Special not found.";
            return Redirect(this.Localized("/admin/specials"));
        }

        special.Title = req.Title.Trim();
        special.Description = req.Description?.Trim();
        special.StartDate = req.StartDate;
        special.EndDate = req.EndDate;
        special.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = $"Special '{special.Title}' updated.";
        return Redirect(this.Localized("/admin/specials"));
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromQuery] bool hard, CancellationToken ct)
    {
        var id = ParseGuid(Request.Form["Id"]);
        var special = await _db.RentSpecials.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (special is null)
        {
            TempData["AdminError"] = "Special not found.";
            return Redirect(this.Localized("/admin/specials"));
        }

        if (hard)
        {
            _db.RentSpecials.Remove(special);
            TempData["AdminSuccess"] = "Special permanently deleted.";
        }
        else
        {
            special.IsActive = false;
            TempData["AdminSuccess"] = "Special deactivated (soft-delete).";
        }
        await _db.SaveChangesAsync(ct);
        return Redirect(this.Localized("/admin/specials"));
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        TotalRows = await _db.RentSpecials.CountAsync(ct);
        if (PageIndex < 1) PageIndex = 1;

        var raw = await _db.RentSpecials
            .AsNoTracking()
            .Select(s => new Row
            {
                Id = s.Id,
                PropertyId = s.PropertyId,
                PropertyTitle = s.Property.Title,
                PropertyCity = s.Property.City,
                Title = s.Title,
                Description = s.Description,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        Rows = raw
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // Property options for dropdown — order by city + title (in memory after pull).
        var propRaw = await _db.Properties
            .AsNoTracking()
            .Select(p => new PropertyOption { Id = p.Id, Title = p.Title, City = p.City })
            .ToListAsync(ct);
        PropertyOptions = propRaw.OrderBy(p => p.City).ThenBy(p => p.Title).ToList();
    }

    private async Task<Row?> LoadEditingRowAsync(Guid id, CancellationToken ct)
    {
        return await _db.RentSpecials
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new Row
            {
                Id = s.Id,
                PropertyId = s.PropertyId,
                PropertyTitle = s.Property.Title,
                PropertyCity = s.Property.City,
                Title = s.Title,
                Description = s.Description,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    private static Guid ParseGuid(Microsoft.Extensions.Primitives.StringValues raw)
        => Guid.TryParse(raw.ToString(), out var g) ? g : Guid.Empty;

    private static DateTimeOffset? ParseDate(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTimeOffset.TryParse(s, out var dt) ? dt : null;
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var s = raw.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static bool ParseBool(Microsoft.Extensions.Primitives.StringValues raw, bool defaultValue)
    {
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        return s == "true" || s == "on" || s == "1";
    }

    public class Row
    {
        public Guid Id { get; set; }
        public Guid PropertyId { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public string PropertyCity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class PropertyOption
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}
