using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class ExternalLoginCallbackModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalLoginCallbackModel> _logger;

    public ExternalLoginCallbackModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalLoginCallbackModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string? remoteError = null, string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            _logger.LogWarning("External provider returned error: {Error}", remoteError);
            TempData["LoginError"] = $"Google sign-in failed: {remoteError}";
            return Redirect("/login");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["LoginError"] = "Could not retrieve external login info. Please try again.";
            return Redirect("/login");
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (signIn.Succeeded)
        {
            _logger.LogInformation("External login {Provider} succeeded for {Key}.", info.LoginProvider, info.ProviderKey);
            return await RedirectAfterSignInAsync(info.GetEmail(), returnUrl);
        }

        var email = info.GetEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                var addLogin = await _userManager.AddLoginAsync(existing, info);
                if (addLogin.Succeeded)
                {
                    await _signInManager.SignInAsync(existing, isPersistent: true);
                    _logger.LogInformation(
                        "Linked external login {Provider} to existing user {Email}.", info.LoginProvider, email);
                    return await RedirectAfterSignInAsync(email, returnUrl);
                }

                _logger.LogWarning(
                    "Failed to link external login for {Email}: {Errors}",
                    email, string.Join(", ", addLogin.Errors.Select(e => e.Description)));
            }
        }

        TempData["ExternalLoginReturnUrl"] = returnUrl;
        return Redirect("/external-login-confirm");
    }

    private async Task<IActionResult> RedirectAfterSignInAsync(string? email, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is not null && await _userManager.IsInRoleAsync(user, Roles.Landlord))
                return Redirect("/landlord");
        }

        return Redirect("/");
    }
}
