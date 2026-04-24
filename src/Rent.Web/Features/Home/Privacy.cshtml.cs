using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Home;

[AllowAnonymous]
public class PrivacyModel : PageModel
{
    public void OnGet() { }
}
