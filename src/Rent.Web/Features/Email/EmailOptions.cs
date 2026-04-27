namespace Rent.Web.Features.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "onboarding@resend.dev";
    public string FromName { get; set; } = "Rent.ca";
    public string BaseUrl { get; set; } = "https://api.resend.com";
}
