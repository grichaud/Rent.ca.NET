using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Favorites.Pages;

[Authorize(Roles = Roles.Renter)]
public class ToggleModel : PageModel
{
    private readonly IFavoriteService _favorites;
    private readonly UserManager<ApplicationUser> _userManager;

    public ToggleModel(IFavoriteService favorites, UserManager<ApplicationUser> userManager)
    {
        _favorites = favorites;
        _userManager = userManager;
    }

    public IActionResult OnGet() => Redirect(this.Localized("/renter/favorites"));

    public async Task<IActionResult> OnPostAsync(
        [FromForm] Guid propertyId,
        [FromForm] string? returnUrl,
        CancellationToken ct)
    {
        if (propertyId == Guid.Empty) return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (!Guid.TryParse(userId, out var uid)) return Forbid();

        var favorited = await _favorites.ToggleAsync(uid, propertyId, ct);

        var wantsJson = Request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        if (wantsJson)
        {
            return new JsonResult(new { favorited });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect(this.Localized("/renter/favorites"));
    }

    public async Task<IActionResult> OnPostRemoveAsync(
        [FromForm] Guid propertyId,
        [FromForm] string? returnUrl,
        CancellationToken ct)
    {
        if (propertyId == Guid.Empty) return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (!Guid.TryParse(userId, out var uid)) return Forbid();

        await _favorites.RemoveAsync(uid, propertyId, ct);
        // Esta ruta no lleva prefijo de cultura, asi que aqui CurrentUICulture no es de fiar:
        // se guarda la clave y la resuelve la vista, que si se sirve bajo /{culture}.
        TempData["FavoriteSuccess"] = "renter.favoriteRemoved";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect(this.Localized("/renter/favorites"));
    }
}
