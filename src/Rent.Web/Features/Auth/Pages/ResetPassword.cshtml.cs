using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<InputModel> _validator;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(
        UserManager<ApplicationUser> userManager,
        IValidator<InputModel> validator,
        ILogger<ResetPasswordModel> logger)
    {
        _userManager = userManager;
        _validator = validator;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // When set, the view renders a "Link expired / request a new one" recovery
    // card instead of the reset form (parity with the Next.js reset page).
    public bool TokenInvalid { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TokenInvalid = true;
            return Page();
        }

        Input.Email = email;
        Input.Token = token;

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            TokenInvalid = true;
            return Page();
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var valid = await _userManager.VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.PasswordResetTokenProvider,
                "ResetPassword",
                decoded);
            if (!valid) TokenInvalid = true;
        }
        catch (FormatException)
        {
            TokenInvalid = true;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName.Replace("Input.", "")}", err.ErrorMessage);
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Same generic outcome whether or not the user exists.
            TempData["LoginSuccess"] = "Password updated. You can now log in.";
            return Redirect(this.Localized("/login"));
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Token));
        }
        catch (FormatException)
        {
            TokenInvalid = true;
            return Page();
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, Input.Password);
        if (!result.Succeeded)
        {
            // An invalid/expired token gets the friendly recovery card; other
            // failures (e.g. password policy) stay on the form with the error.
            if (result.Errors.Any(e => e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase)))
            {
                TokenInvalid = true;
                return Page();
            }
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        _logger.LogInformation("Password reset for {Email}.", user.Email);
        TempData["LoginSuccess"] = "Password updated. You can now log in.";
        return Redirect(this.Localized("/login"));
    }

    public class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
