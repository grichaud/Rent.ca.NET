using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Features.Email;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class ExternalLoginConfirmModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IValidator<InputModel> _validator;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ExternalLoginConfirmModel> _logger;

    public ExternalLoginConfirmModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IValidator<InputModel> validator,
        IEmailSender emailSender,
        ILogger<ExternalLoginConfirmModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _validator = validator;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string Email { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;

    private const string SessionExpiredMessage =
        "Your sign-up session expired before you finished. Please click \"Continue with Google\" again.";

    public async Task<IActionResult> OnGetAsync()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["LoginError"] = SessionExpiredMessage;
            return Redirect(this.Localized("/login"));
        }

        Email = info.GetEmail() ?? string.Empty;
        Provider = info.ProviderDisplayName ?? info.LoginProvider;
        Input.FullName = info.GetFullName() ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            _logger.LogWarning("ExternalLoginConfirm POST: external cookie missing/expired before user submitted.");
            TempData["LoginError"] = SessionExpiredMessage;
            return Redirect(this.Localized("/login"));
        }

        Email = info.GetEmail() ?? string.Empty;
        Provider = info.ProviderDisplayName ?? info.LoginProvider;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(string.Empty, "External provider did not return an email address.");
            return Page();
        }

        var validation = await _validator.ValidateAsync(Input, ct);
        if (!validation.IsValid)
        {
            foreach (var err in validation.Errors)
                ModelState.AddModelError($"Input.{err.PropertyName.Replace("Input.", "")}", err.ErrorMessage);
            return Page();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = Email,
            UserName = Email,
            FullName = Input.FullName,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await _userManager.AddToRoleAsync(user, Input.Role);

        if (Input.Role == Roles.Landlord)
        {
            _db.LandlordProfiles.Add(new LandlordProfile
            {
                Id = user.Id,
                Tier = ListingTier.Limited
            });
            await _db.SaveChangesAsync(ct);
        }

        var addLogin = await _userManager.AddLoginAsync(user, info);
        if (!addLogin.Succeeded)
        {
            foreach (var e in addLogin.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        _logger.LogInformation(
            "User {Email} created via {Provider} with role {Role}.", user.Email, info.LoginProvider, Input.Role);

        var portalPath = Input.Role switch
        {
            Roles.Landlord => "/landlord",
            Roles.Renter => "/renter",
            _ => "/"
        };

        var localizedPortal = this.Localized(portalPath);
        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            await _emailSender.SendWelcomeAsync(
                new WelcomeEmail(user.Email!, user.FullName ?? string.Empty, Input.Role, $"{origin}{localizedPortal}", this.CurrentCulture()), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
        }

        return Redirect(localizedPortal);
    }

    public class InputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.Renter;
    }
}
