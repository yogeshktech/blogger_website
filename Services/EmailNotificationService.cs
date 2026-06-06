using Microsoft.AspNetCore.Identity.UI.Services;

namespace Blogger_website.Services;

public interface IEmailNotificationService
{
    Task SendAccountApprovedAsync(string email, string? fullName, string loginUrl);
}

public class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailSender _emailSender;

    public EmailNotificationService(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendAccountApprovedAsync(string email, string? fullName, string loginUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(fullName) ? "Admin" : fullName.Trim();
        var body = $@"
<h2>Account Approved — BlogHub</h2>
<p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
<p>Great news! Your admin account has been approved by the SuperAdmin.</p>
<p>You can now sign in and start publishing blogs.</p>
<p><a href=""{System.Net.WebUtility.HtmlEncode(loginUrl)}"" style=""display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;"">Sign In to Admin Panel</a></p>
<p>If the button does not work, copy this link:<br/>{System.Net.WebUtility.HtmlEncode(loginUrl)}</p>";

        return _emailSender.SendEmailAsync(email, "Your admin account is approved — BlogHub", body);
    }
}
