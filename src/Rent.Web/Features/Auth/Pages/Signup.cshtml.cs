using FluentValidation;
using Microsoft.AspNetCore.Authentication;
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
public class SignupModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IValidator<InputModel> _validator;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SignupModel> _logger;

    public SignupModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IValidator<InputModel> validator,
        IEmailSender emailSender,
        ILogger<SignupModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _validator = validator;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme> ExternalLogins { get; private set; } = new List<AuthenticationScheme>();

    public async Task OnGetAsync()
    {
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
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
            Email = Input.Email,
            UserName = Input.Email,
            FullName = Input.FullName
        };

        var create = await _userManager.CreateAsync(user, Input.Password);
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

        await _signInManager.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("User {Email} signed up with role {Role}.", user.Email, Input.Role);

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

    public IActionResult OnPostExternalLogin(string provider, string? returnUrl = null)
    {
        // See LoginModel.OnPostExternalLogin for why we hardcode the URL instead of using Url.Page.
        var redirectUrl = string.IsNullOrEmpty(returnUrl)
            ? "/external-login-callback"
            : $"/external-login-callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    public class InputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.Renter;
    }
}
