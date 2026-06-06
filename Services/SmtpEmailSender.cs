using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Blogger_website.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.Username))
        {
            _logger.LogWarning("SMTP not configured; email to {Email} was not sent.", email);
            return;
        }

        var username = _options.Username.Trim();
        var password = NormalizeAppPassword(_options.Password);
        var fromEmail = string.IsNullOrWhiteSpace(_options.FromEmail) ? username : _options.FromEmail.Trim();

        if (string.IsNullOrEmpty(password))
        {
            _logger.LogError("SMTP Password is empty for {Username}. Update appsettings.json and rebuild.", username);
            throw new InvalidOperationException("SMTP password is not configured.");
        }

        if (!string.Equals(username, fromEmail, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "SMTP Username ({Username}) and FromEmail ({FromEmail}) differ. Gmail requires they match.",
                username, fromEmail);
        }

        _logger.LogDebug("SMTP auth for {Username}, app password length {Length}", username, password.Length);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var client = new SmtpClient();
        var secureOption = _options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, secureOption);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Email sent to {Email}: {Subject}", email, subject);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex,
                "Gmail rejected SMTP login for {Username}. Use a 16-char App Password (not Gmail login password), " +
                "then stop the app, run 'dotnet clean' + rebuild so bin/appsettings.json updates.",
                username);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP failed for {Email} using {Username}.", email, username);
            throw;
        }
    }

    private static string NormalizeAppPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return string.Empty;

        return new string(password.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
