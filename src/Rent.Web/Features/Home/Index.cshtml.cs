using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Home;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<City> FeaturedCities { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        FeaturedCities = await _db.Cities
            .Where(c => c.IsFeatured)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }
}
