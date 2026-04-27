using System.Net;
using System.Text;

namespace Rent.Web.Features.Email;

public static class EmailTemplates
{
    private const string Brand = "#338dff";
    private const string BrandDark = "#142857";
    private const string LightBg = "#f8fafc";
    private const string CardBg = "#ffffff";
    private const string BodyText = "#0f172a";
    private const string MutedText = "#475569";

    public static (string Subject, string Html) Inquiry(InquiryEmail data)
    {
        var subject = $"New lead for \"{data.PropertyTitle}\"";
        var moveIn = data.MoveInDate is null ? "Not specified" : data.MoveInDate.Value.ToString("MMM d, yyyy");
        var phoneRow = string.IsNullOrWhiteSpace(data.SenderPhone)
            ? string.Empty
            : Row("Phone", Encode(data.SenderPhone));

        var body = new StringBuilder();
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 8px;'>Hi {Encode(data.LandlordName)},</p>");
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 24px;'>You have a new lead for <strong>{Encode(data.PropertyTitle)}</strong>.</p>");
        body.Append($"<table style='width:100%;border-collapse:collapse;margin:0 0 24px;'>");
        body.Append(Row("From", $"{Encode(data.SenderName)} &lt;{Encode(data.SenderEmail)}&gt;"));
        body.Append(phoneRow);
        body.Append(Row("Move-in", Encode(moveIn)));
        body.Append("</table>");
        body.Append($"<div style='border-left:3px solid {Brand};padding:8px 16px;margin:0 0 24px;color:{BodyText};font-size:15px;'>");
        body.Append(Encode(data.Message).Replace("\n", "<br/>"));
        body.Append("</div>");
        body.Append(Buttons(("Open inbox", data.InboxUrl, true), ("View listing", data.PropertyUrl, false)));

        return (subject, Wrap(subject, body.ToString()));
    }

    public static (string Subject, string Html) Welcome(WelcomeEmail data)
    {
        var subject = "Welcome to Rent.ca";
        var greeting = string.IsNullOrWhiteSpace(data.ToName) ? "Hi there" : $"Hi {Encode(data.ToName)}";
        var roleCopy = data.Role == "Landlord"
            ? "Your landlord dashboard is ready. Post your first listing and start receiving leads."
            : "Browse listings, save your favourites, and message landlords directly.";
        var ctaText = data.Role == "Landlord" ? "Open landlord dashboard" : "Start browsing";

        var body = new StringBuilder();
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 12px;'>{greeting},</p>");
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 24px;'>Welcome to Rent.ca. {roleCopy}</p>");
        body.Append(Buttons((ctaText, data.PortalUrl, true)));

        return (subject, Wrap(subject, body.ToString()));
    }

    public static (string Subject, string Html) PasswordReset(PasswordResetEmail data)
    {
        var subject = "Reset your Rent.ca password";
        var greeting = string.IsNullOrWhiteSpace(data.ToName) ? "Hi there" : $"Hi {Encode(data.ToName)}";

        var body = new StringBuilder();
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 12px;'>{greeting},</p>");
        body.Append($"<p style='font-size:16px;color:{BodyText};margin:0 0 12px;'>Tap the button below to choose a new password. The link expires in a few hours.</p>");
        body.Append(Buttons(("Reset password", data.ResetUrl, true)));
        body.Append($"<p style='font-size:13px;color:{MutedText};margin:24px 0 0;'>If you didn&rsquo;t request this, you can safely ignore this email.</p>");

        return (subject, Wrap(subject, body.ToString()));
    }

    private static string Wrap(string title, string innerHtml) => $@"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>{Encode(title)}</title>
</head>
<body style=""margin:0;padding:24px 0;background-color:{LightBg};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;"">
  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"" style=""max-width:560px;width:100%;background-color:{CardBg};border-radius:16px;overflow:hidden;box-shadow:0 8px 24px rgba(15,23,42,0.06);"">
    <tr>
      <td style=""padding:24px 32px;background:linear-gradient(135deg,{Brand},{BrandDark});color:#fff;font-weight:600;font-size:18px;letter-spacing:.2px;"">Rent.ca</td>
    </tr>
    <tr>
      <td style=""padding:32px;"">
        {innerHtml}
      </td>
    </tr>
    <tr>
      <td style=""padding:16px 32px 24px;color:{MutedText};font-size:12px;border-top:1px solid #e2e8f0;"">Sent by Rent.ca &middot; <a href=""https://rent-ca-net.azurewebsites.net"" style=""color:{Brand};text-decoration:none;"">rent-ca-net.azurewebsites.net</a></td>
    </tr>
  </table>
</body>
</html>";

    private static string Row(string label, string value) =>
        $"<tr><td style='padding:6px 0;color:{MutedText};font-size:13px;width:90px;'>{Encode(label)}</td><td style='padding:6px 0;color:{BodyText};font-size:14px;'>{value}</td></tr>";

    private static string Buttons(params (string Text, string Url, bool Primary)[] buttons)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='display:block;'>");
        foreach (var (text, url, primary) in buttons)
        {
            var bg = primary ? Brand : "#e2e8f0";
            var fg = primary ? "#ffffff" : BodyText;
            sb.Append($"<a href='{Encode(url)}' style='display:inline-block;padding:12px 20px;margin:0 8px 8px 0;border-radius:10px;background-color:{bg};color:{fg};font-weight:600;font-size:14px;text-decoration:none;'>{Encode(text)}</a>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
