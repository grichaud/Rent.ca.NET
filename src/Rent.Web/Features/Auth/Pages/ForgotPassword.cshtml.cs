using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Auth.Pages;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    public void OnGet() { }
}
