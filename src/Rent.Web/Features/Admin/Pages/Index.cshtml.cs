using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.Admin.Pages;

[Authorize(Roles = Roles.Admin)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public int TotalProperties { get; private set; }
    public int FeaturedProperties { get; private set; }
    public int PromotedProperties { get; private set; }
    public int TotalLandlords { get; private set; }
    public int ActiveSpecials { get; private set; }
    public int Conversations { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        TotalProperties = await _db.Properties.CountAsync(ct);
        FeaturedProperties = await _db.Properties.CountAsync(p => p.Tier == ListingTier.Featured, ct);
        PromotedProperties = await _db.Properties.CountAsync(p => p.Tier == ListingTier.Promoted, ct);
        TotalLandlords = await _db.LandlordProfiles.CountAsync(ct);
        ActiveSpecials = await _db.RentSpecials.CountAsync(s => s.IsActive, ct);
        Conversations = await _db.AiConversations.CountAsync(ct);
    }
}
