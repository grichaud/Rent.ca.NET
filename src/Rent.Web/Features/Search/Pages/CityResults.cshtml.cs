using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;

namespace Rent.Web.Features.Search.Pages;

[AllowAnonymous]
public class CityResultsModel : PageModel
{
    private readonly SearchHandler _search;
    private readonly UserManager<ApplicationUser> _userManager;

    public CityResultsModel(SearchHandler search, UserManager<ApplicationUser> userManager)
    {
        _search = search;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public SearchQuery Query { get; set; } = new();

    public SearchResult Result { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string citySlug, CancellationToken ct)
    {
        Query.CitySlug = citySlug;
        Guid? uid = null;
        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(_userManager.GetUserId(User), out var parsed)) uid = parsed;
        Result = await _search.ExecuteAsync(Query, uid, ct);
        if (Result.City is null)
            Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }
}
