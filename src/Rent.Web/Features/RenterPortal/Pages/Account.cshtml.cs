using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.RenterPortal.Pages;

[Authorize(Roles = Roles.Renter)]
public class AccountModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IValidator<ProfileInput> _profileValidator;
    private readonly IValidator<PasswordInput> _passwordValidator;
    private readonly ILogger<AccountModel> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AccountModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IValidator<ProfileInput> profileValidator,
        IValidator<PasswordInput> passwordValidator,
        ILogger<AccountModel> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _profileValidator = profileValidator;
        _passwordValidator = passwordValidator;
        _logger = logger;
        _localizer = localizer;
    }

    [BindProperty]
    public ProfileInput Profile { get; set; } = new();

    [BindProperty]
    public PasswordInput Password { get; set; } = new();

    public string Email { get; private set; } = string.Empty;
    public bool HasPassword { get; private set; }
    public string? FlashSuccess { get; private set; }
    public string? FlashError { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        Email = user.Email ?? string.Empty;
        Profile.FullName = user.FullName ?? string.Empty;
        Profile.Phone = user.PhoneNumber;
        HasPassword = await _userManager.HasPasswordAsync(user);

        FlashSuccess = TempData["AccountSuccess"] as string;
        FlashError = TempData["AccountError"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        Email = user.Email ?? string.Empty;
        HasPassword = await _userManager.HasPasswordAsync(user);

        var validation = await _profileValidator.ValidateAsync(Profile, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Profile.{err.PropertyName.Replace("Profile.", "")}", err.ErrorMessage);
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
            return Page();
        }

        TempData["AccountSuccess"] = _localizer["renter.accountProfileSaved"].Value;
        return Redirect(this.Localized("/renter/account"));
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        Email = user.Email ?? string.Empty;
        Profile.FullName = user.FullName ?? string.Empty;
        Profile.Phone = user.PhoneNumber;
        HasPassword = await _userManager.HasPasswordAsync(user);

        if (!HasPassword)
        {
            // Google OAuth user — no local password to change.
            TempData["AccountError"] = _localizer["renter.accountGoogle"].Value;
            return Redirect(this.Localized("/renter/account"));
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
                // PasswordMismatch es el unico caso que el usuario puede accionar: dale copy localizado.
                var message = e.Code == "PasswordMismatch"
                    ? _localizer["renter.accountIncorrectCurrent"].Value
                    : e.Description;
                ModelState.AddModelError("Password", message);
            }
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("User {UserId} changed password.", user.Id);
        TempData["AccountSuccess"] = _localizer["renter.accountPasswordChanged"].Value;
        return Redirect(this.Localized("/renter/account"));
    }

    public class ProfileInput
    {
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class PasswordInput
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
