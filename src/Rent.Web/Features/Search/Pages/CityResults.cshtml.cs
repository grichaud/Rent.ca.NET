using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Search.Pages;

[AllowAnonymous]
public class CityResultsModel : PageModel
{
    private readonly SearchHandler _search;

    public CityResultsModel(SearchHandler search)
    {
        _search = search;
    }

    [BindProperty(SupportsGet = true)]
    public SearchQuery Query { get; set; } = new();

    public SearchResult Result { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string citySlug, CancellationToken ct)
    {
        Query.CitySlug = citySlug;
        Result = await _search.ExecuteAsync(Query, ct);
        if (Result.City is null)
            Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }
}
