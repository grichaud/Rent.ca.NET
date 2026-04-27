using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Favorites.Pages;

[Authorize(Roles = Roles.Renter)]
public class IndexModel : PageModel
{
    private readonly IFavoriteService _favorites;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(IFavoriteService favorites, UserManager<ApplicationUser> userManager)
    {
        _favorites = favorites;
        _userManager = userManager;
    }

    public IReadOnlyList<FavoriteCard> Favorites { get; private set; } = Array.Empty<FavoriteCard>();
    public string? FlashSuccess { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        FlashSuccess = TempData["FavoriteSuccess"] as string;
        var userId = _userManager.GetUserId(User);
        if (!Guid.TryParse(userId, out var uid)) return;

        Favorites = await _favorites.ListAsync(uid, ct);
    }
}
