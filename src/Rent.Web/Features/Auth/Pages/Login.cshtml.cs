using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; private set; } = new List<AuthenticationScheme>();

    public string? StatusMessage => TempData["LoginError"] as string;
    public string? SuccessMessage => TempData["LoginSuccess"] as string;

    public async Task OnGetAsync()
    {
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        if (!ModelState.IsValid) return Page();

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return Redirect(ReturnUrl);

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is not null)
        {
            if (await _userManager.IsInRoleAsync(user, Roles.Landlord))
                return Redirect(this.Localized("/landlord"));
            if (await _userManager.IsInRoleAsync(user, Roles.Renter))
                return Redirect(this.Localized("/renter"));
        }

        return Redirect(this.Localized("/"));
    }

    public IActionResult OnPostExternalLogin(string provider, string? returnUrl = null)
    {
        // Use the literal route from ExternalLoginCallback's @page directive — Url.Page("/ExternalLoginCallback")
        // returns null because RootDirectory='/Features' makes the page name '/Auth/Pages/ExternalLoginCallback'.
        var redirectUrl = string.IsNullOrEmpty(returnUrl)
            ? "/external-login-callback"
            : $"/external-login-callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
