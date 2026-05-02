using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Features.AiChat.Services;

namespace Rent.Web.Features.AiChat.Pages;

[AllowAnonymous]
public class ConversationsModel : PageModel
{
    private readonly IAiChatService _chat;
    private readonly UserManager<ApplicationUser> _userManager;

    public ConversationsModel(IAiChatService chat, UserManager<ApplicationUser> userManager)
    {
        _chat = chat;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var sessionId = AiSessionCookie.EnsureSessionId(HttpContext);
        Guid? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var raw = _userManager.GetUserId(User);
            if (Guid.TryParse(raw, out var uid)) userId = uid;
        }

        var active = await _chat.GetActiveConversationAsync(userId, sessionId, ct);
        if (active is null)
            return new JsonResult(new { conversation = (object?)null });

        return new JsonResult(new
        {
            conversation = new
            {
                id = active.ConversationId,
                title = active.Title,
                updatedAt = active.UpdatedAt,
                messages = active.Messages.Select(m => new
                {
                    id = m.Id,
                    role = m.Role.ToLowerInvariant(),
                    content = m.Content,
                    createdAt = m.CreatedAt
                })
            }
        });
    }
}
