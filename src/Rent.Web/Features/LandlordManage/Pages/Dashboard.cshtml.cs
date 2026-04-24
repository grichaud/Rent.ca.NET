using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Identity;

namespace Rent.Web.Features.LandlordManage.Pages;

[Authorize(Roles = Roles.Landlord)]
public class DashboardModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string FullName { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        FullName = user?.FullName ?? user?.Email ?? "landlord";
    }
}
