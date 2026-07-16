using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

// El cambio de contrasena reusa el input y el validador ya aprobados del portal renter.
using RenterAccount = Rent.Web.Features.RenterPortal.Pages.AccountModel;

namespace Rent.Web.Features.LandlordManage.Pages;

[Authorize(Roles = Roles.Landlord)]
public partial class AccountModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IValidator<LandlordProfileInput> _profileValidator;
    private readonly IValidator<RenterAccount.PasswordInput> _passwordValidator;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AccountModel> _logger;

    public AccountModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IValidator<LandlordProfileInput> profileValidator,
        IValidator<RenterAccount.PasswordInput> passwordValidator,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _profileValidator = profileValidator;
        _passwordValidator = passwordValidator;
        _localizer = localizer;
        _logger = logger;
    }

    // El seeder guarda su centinela de version DENTRO de LandlordProfile.Description. Si el
    // landlord demo edita su bio y el tag desaparece, el siguiente arranque borra y recrea todo
    // el catalogo demo (cascada a Units/Images/Inquiries/Favorites). Se preserva al guardar.
    [GeneratedRegex(@"\s*\[seed:[^\]]+\]\s*$")]
    private static partial Regex SeedTagRegex();

    [BindProperty]
    public LandlordProfileInput Profile { get; set; } = new();

    [BindProperty]
    public RenterAccount.PasswordInput Password { get; set; } = new();

    public string Email { get; private set; } = string.Empty;
    public bool HasPassword { get; private set; }
    public bool IsVerified { get; private set; }
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        await LoadAsync(user, ct);
        FlashSuccess = TempData["AccountSuccess"] as string;
        FlashError = TempData["AccountError"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var profile = await _db.LandlordProfiles.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        if (profile is null) return Forbid();

        var validation = await _profileValidator.ValidateAsync(Profile, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Profile.{err.PropertyName}", err.ErrorMessage);
            await LoadAsync(user, ct, keepInput: true);
            return Page();
        }

        user.FullName = Profile.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Profile.Phone) ? null : Profile.Phone.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors)
                ModelState.AddModelError("Profile", e.Description);
            await LoadAsync(user, ct, keepInput: true);
            return Page();
        }

        profile.CompanyName = string.IsNullOrWhiteSpace(Profile.CompanyName) ? null : Profile.CompanyName.Trim();
        profile.Website = string.IsNullOrWhiteSpace(Profile.Website) ? null : Profile.Website.Trim();

        var seedTag = SeedTagRegex().Match(profile.Description ?? string.Empty).Value;
        var bio = string.IsNullOrWhiteSpace(Profile.Description) ? null : Profile.Description.Trim();
        profile.Description = string.IsNullOrEmpty(seedTag) ? bio : (bio + seedTag);

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["AccountSuccess"] = _localizer["landlord.accountBrandSaved"].Value;
        return Redirect(this.Localized("/landlord/account"));
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        await LoadAsync(user, ct);

        if (!HasPassword)
        {
            TempData["AccountError"] = _localizer["renter.accountGoogle"].Value;
            return Redirect(this.Localized("/landlord/account"));
        }

        var validation = await _passwordValidator.ValidateAsync(Password, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Password.{err.PropertyName.Replace("Password.", "")}", err.ErrorMessage);
            return Page();
        }

        var change = await _userManager.ChangePasswordAsync(user, Password.CurrentPassword, Password.NewPassword);
        if (!change.Succeeded)
        {
            foreach (var e in change.Errors)
            {
                var message = e.Code == "PasswordMismatch"
                    ? _localizer["renter.accountIncorrectCurrent"].Value
                    : e.Description;
                ModelState.AddModelError("Password", message);
            }
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("Landlord {UserId} changed password.", user.Id);
        TempData["AccountSuccess"] = _localizer["renter.accountPasswordChanged"].Value;
        return Redirect(this.Localized("/landlord/account"));
    }

    private async Task LoadAsync(ApplicationUser user, CancellationToken ct, bool keepInput = false)
    {
        Email = user.Email ?? string.Empty;
        HasPassword = await _userManager.HasPasswordAsync(user);

        var profile = await _db.LandlordProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        IsVerified = profile?.IsVerified ?? false;

        if (keepInput) return;

        Profile = new LandlordProfileInput
        {
            FullName = user.FullName ?? string.Empty,
            Phone = user.PhoneNumber,
            CompanyName = profile?.CompanyName,
            Website = profile?.Website,
            // El centinela del seeder no se le muestra al usuario.
            Description = SeedTagRegex().Replace(profile?.Description ?? string.Empty, string.Empty)
        };
    }

    public class LandlordProfileInput
    {
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
    }
}
