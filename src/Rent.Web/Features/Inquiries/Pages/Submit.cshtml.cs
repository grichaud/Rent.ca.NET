using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.Inquiries.Pages;

[AllowAnonymous]
public class SubmitModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<InquiryRequest> _validator;
    private readonly ILogger<SubmitModel> _logger;

    public SubmitModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IValidator<InquiryRequest> validator,
        ILogger<SubmitModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
        _logger = logger;
    }

    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync([FromForm] InquiryRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            TempData["InquiryError"] = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return RedirectBackToListing(request);
        }

        var exists = await _db.Properties
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PropertyId && p.Status == ListingStatus.Active, ct);
        if (!exists)
        {
            TempData["InquiryError"] = "This listing is no longer accepting inquiries.";
            return RedirectBackToListing(request);
        }

        Guid? senderUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _userManager.GetUserId(User);
            if (Guid.TryParse(userId, out var gid))
                senderUserId = gid;
        }

        _db.ContactInquiries.Add(new ContactInquiry
        {
            Id = Guid.NewGuid(),
            PropertyId = request.PropertyId,
            SenderUserId = senderUserId,
            SenderName = request.SenderName.Trim(),
            SenderEmail = request.SenderEmail.Trim(),
            SenderPhone = string.IsNullOrWhiteSpace(request.SenderPhone) ? null : request.SenderPhone.Trim(),
            Message = request.Message.Trim(),
            MoveInDate = request.MoveInDate,
            IsRead = false
        });

        await _db.Properties
            .Where(p => p.Id == request.PropertyId)
            .ExecuteUpdateAsync(s => s.SetProperty(pp => pp.LeadCount, pp => pp.LeadCount + 1), ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Inquiry submitted for property {PropertyId} by {Email}", request.PropertyId, request.SenderEmail);

        TempData["InquirySuccess"] = "Your message has been sent to the landlord.";
        return RedirectBackToListing(request);
    }

    private IActionResult RedirectBackToListing(InquiryRequest req)
    {
        if (!string.IsNullOrEmpty(req.ReturnCitySlug) && !string.IsNullOrEmpty(req.ReturnPropertySlug))
            return Redirect($"/{req.ReturnCitySlug}/{req.ReturnPropertySlug}");
        return Redirect("/");
    }
}
