using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Home;

[AllowAnonymous]
public class AboutModel : PageModel
{
    public void OnGet() { }
}
