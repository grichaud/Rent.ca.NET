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

    public IActionResult OnGet(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            return Redirect(this.Localized("/login"));

        Input.Email = email;
        Input.Token = token;
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
            ModelState.AddModelError(string.Empty, "Reset link is invalid or expired.");
            return Page();
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, Input.Password);
        if (!result.Succeeded)
        {
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
