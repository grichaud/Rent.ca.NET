using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Admin.Pages;

[Authorize(Roles = Roles.Admin)]
public class AiConversationModel : PageModel
{
    private readonly AppDbContext _db;

    public AiConversationModel(AppDbContext db)
    {
        _db = db;
    }

    public AiConversation? Conversation { get; private set; }
    public string? UserEmail { get; private set; }
    public IReadOnlyList<AiMessage> Messages { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Conversation = await _db.AiConversations
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (Conversation is null)
        {
            return NotFound();
        }

        UserEmail = Conversation.User?.Email;

        var raw = await _db.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == id)
            .ToListAsync(ct);
        Messages = raw.OrderBy(m => m.CreatedAt).ToList();

        return Page();
    }
}
