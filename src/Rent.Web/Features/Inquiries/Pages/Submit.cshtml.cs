using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Features.Email;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Localization;

namespace Rent.Web.Features.Inquiries.Pages;

[AllowAnonymous]
public class SubmitModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<InquiryRequest> _validator;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SubmitModel> _logger;

    public SubmitModel(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IValidator<InquiryRequest> validator,
        IEmailSender emailSender,
        ILogger<SubmitModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _validator = validator;
        _emailSender = emailSender;
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

        var property = await _db.Properties
            .AsNoTracking()
            .Include(p => p.LandlordProfile)
                .ThenInclude(lp => lp.User)
            .FirstOrDefaultAsync(p => p.Id == request.PropertyId && p.Status == ListingStatus.Active, ct);
        if (property is null)
        {
            TempData["InquiryError"] = "detail.inquiryListingInactive";
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

        try
        {
            var landlordEmail = property.LandlordProfile?.User?.Email;
            if (!string.IsNullOrWhiteSpace(landlordEmail))
            {
                var origin = $"{Request.Scheme}://{Request.Host}";
                var propertyUrl = !string.IsNullOrEmpty(request.ReturnCitySlug) && !string.IsNullOrEmpty(request.ReturnPropertySlug)
                    ? $"{origin}/{request.ReturnCitySlug}/{request.ReturnPropertySlug}"
                    : origin;

                await _emailSender.SendInquiryToLandlordAsync(new InquiryEmail(
                    LandlordEmail: landlordEmail,
                    LandlordName: property.LandlordProfile?.User?.FullName ?? "there",
                    PropertyTitle: property.Title,
                    PropertyUrl: propertyUrl,
                    InboxUrl: $"{origin}/landlord/inbox",
                    SenderName: request.SenderName.Trim(),
                    SenderEmail: request.SenderEmail.Trim(),
                    SenderPhone: string.IsNullOrWhiteSpace(request.SenderPhone) ? null : request.SenderPhone.Trim(),
                    Message: request.Message.Trim(),
                    MoveInDate: request.MoveInDate), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send inquiry email for property {PropertyId}", request.PropertyId);
        }

        TempData["InquirySuccess"] = "detail.inquirySent";
        return RedirectBackToListing(request);
    }

    // Esta ruta no lleva prefijo de cultura: this.Localized() caeria al idioma por defecto y
    // expulsaria a /en a quien envio desde /fr. El formulario nos dice de donde vino.
    private IActionResult RedirectBackToListing(InquiryRequest req)
    {
        var culture = req.ReturnCulture is "en" or "fr" ? req.ReturnCulture : LocalizationConfig.DefaultCulture;

        if (!string.IsNullOrEmpty(req.ReturnCitySlug) && !string.IsNullOrEmpty(req.ReturnPropertySlug))
            return Redirect($"/{culture}/{req.ReturnCitySlug}/{req.ReturnPropertySlug}");
        return Redirect($"/{culture}");
    }
}
