using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class SignupModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IValidator<InputModel> _validator;
    private readonly ILogger<SignupModel> _logger;

    public SignupModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IValidator<InputModel> validator,
        ILogger<SignupModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
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

        return Input.Role == Roles.Landlord ? Redirect("/landlord") : Redirect("/");
    }

    public class InputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.Renter;
    }
}
