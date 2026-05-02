using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Features.AiChat.Services;

namespace Rent.Web.Features.AiChat.Pages;

[AllowAnonymous]
public class NewConversationModel : PageModel
{
    public IActionResult OnGet() => StatusCode(405);

    public IActionResult OnPost()
    {
        AiSessionCookie.EnsureSessionId(HttpContext);
        return new JsonResult(new { ok = true });
    }
}
