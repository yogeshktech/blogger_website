using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Blogger_website.Services;

public interface IEmailOtpService
{
    Task SendRegistrationOtpAsync(string email, string? fullName);
    Task<bool> VerifyRegistrationOtpAsync(string email, string otp);
    bool CanResend(string email);
}

public class EmailOtpService : IEmailOtpService
{
    private const int OtpLength = 6;
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;

    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailOtpService> _logger;

    public EmailOtpService(IMemoryCache cache, IEmailSender emailSender, ILogger<EmailOtpService> logger)
    {
        _cache = cache;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendRegistrationOtpAsync(string email, string? fullName)
    {
        var normalizedEmail = NormalizeEmail(email);
        var otp = GenerateOtp();
        var entry = new OtpEntry(HashOtp(otp), DateTime.UtcNow.Add(OtpLifetime), 0);

        _cache.Set(OtpCacheKey(normalizedEmail), entry, OtpLifetime);
        _cache.Set(ResendCacheKey(normalizedEmail), true, ResendCooldown);

        var displayName = string.IsNullOrWhiteSpace(fullName) ? "Admin" : fullName.Trim();
        var body = $@"
<h2>Email Verification — BlogHub</h2>
<p>Hi {WebUtilityEncode(displayName)},</p>
<p>Your verification code is:</p>
<p style=""font-size:28px;font-weight:bold;letter-spacing:4px;color:#4f46e5;"">{otp}</p>
<p>This code expires in 10 minutes. Do not share it with anyone.</p>
<p>If you did not register, you can ignore this email.</p>";

        await _emailSender.SendEmailAsync(normalizedEmail, "Verify your email — BlogHub", body);
        _logger.LogInformation("Registration OTP sent to {Email}", normalizedEmail);
    }

    public Task<bool> VerifyRegistrationOtpAsync(string email, string otp)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!_cache.TryGetValue(OtpCacheKey(normalizedEmail), out OtpEntry? entry) || entry == null)
            return Task.FromResult(false);

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _cache.Remove(OtpCacheKey(normalizedEmail));
            return Task.FromResult(false);
        }

        if (entry.Attempts >= MaxAttempts)
            return Task.FromResult(false);

        entry.Attempts++;
        _cache.Set(OtpCacheKey(normalizedEmail), entry, entry.ExpiresAt - DateTime.UtcNow);

        if (!string.Equals(entry.CodeHash, HashOtp(otp.Trim()), StringComparison.Ordinal))
            return Task.FromResult(false);

        _cache.Remove(OtpCacheKey(normalizedEmail));
        return Task.FromResult(true);
    }

    public bool CanResend(string email)
        => !_cache.TryGetValue(ResendCacheKey(NormalizeEmail(email)), out _);

    private static string GenerateOtp()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashOtp(string otp)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string OtpCacheKey(string email) => $"reg-otp:{email}";
    private static string ResendCacheKey(string email) => $"reg-otp-resend:{email}";

    private static string WebUtilityEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private sealed class OtpEntry
    {
        public OtpEntry(string codeHash, DateTime expiresAt, int attempts)
        {
            CodeHash = codeHash;
            ExpiresAt = expiresAt;
            Attempts = attempts;
        }

        public string CodeHash { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
    }
}
