using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Home;

[AllowAnonymous]
public class LandlordsLandingModel : PageModel
{
    public void OnGet() { }
}
