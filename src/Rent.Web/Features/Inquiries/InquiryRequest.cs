namespace Rent.Web.Features.Inquiries;

public class InquiryRequest
{
    public Guid PropertyId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateOnly? MoveInDate { get; set; }
    public string? ReturnCitySlug { get; set; }
    public string? ReturnPropertySlug { get; set; }
    public string? ReturnCulture { get; set; }
}
