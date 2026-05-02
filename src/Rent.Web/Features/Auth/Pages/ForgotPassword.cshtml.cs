using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Rent.Web.Domain;
using Rent.Web.Features.Email;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private const string GenericMessage =
        "If an account with that email exists, we've sent a link to reset your password.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Submitted { get; private set; }

    public string Message => GenericMessage;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null && !string.IsNullOrEmpty(user.Email))
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var origin = $"{Request.Scheme}://{Request.Host}";
                var locale = this.CurrentCulture();
                var resetUrl = $"{origin}/{locale}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={encoded}";

                await _emailSender.SendPasswordResetAsync(
                    new PasswordResetEmail(user.Email, user.FullName ?? string.Empty, resetUrl, locale), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset email to {Email}", user.Email);
            }
        }
        else
        {
            _logger.LogInformation("Password reset requested for unknown email {Email}; suppressed.", email);
        }

        Submitted = true;
        return Page();
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
