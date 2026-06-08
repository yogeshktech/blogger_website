using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Identity;

namespace Blogger_website.Services;

public interface IAdminRegistrationService
{
    Task<RegistrationResult> RegisterAsync(string email, string password, string fullName);
    Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp);
    Task<(bool Success, string Message)> ResendOtpAsync(string email);
}

public record RegistrationResult(bool Success, string Message, string? Email = null, bool RequiresOtp = false);

public class AdminRegistrationService : IAdminRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailOtpService _otpService;

    public AdminRegistrationService(UserManager<ApplicationUser> userManager, IEmailOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<RegistrationResult> RegisterAsync(string email, string password, string fullName)
    {
        email = email.Trim();
        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return new RegistrationResult(false, "Email and Password are required");

        if (string.IsNullOrWhiteSpace(fullName))
            return new RegistrationResult(false, "Full Name is required");

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            if (!existing.EmailConfirmed)
            {
                await _otpService.SendRegistrationOtpAsync(existing.Email!, existing.FullName);
                return new RegistrationResult(true, "Verification code sent to your email.", existing.Email, true);
            }

            return new RegistrationResult(false, "Email already registered");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = false,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return new RegistrationResult(false, string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, AppRoles.Blogger);
        await _otpService.SendRegistrationOtpAsync(user.Email!, user.FullName);

        return new RegistrationResult(
            true,
            "Registration successful. Enter the OTP sent to your email to verify.",
            user.Email,
            true);
    }

    public async Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            return (false, "Email and OTP are required");

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
            return (false, "Account not found");

        if (user.EmailConfirmed)
            return (true, "Email already verified. You can sign in.");

        if (!await _otpService.VerifyRegistrationOtpAsync(email, otp))
            return (false, "Invalid or expired OTP. Please try again or resend.");

        user.EmailConfirmed = true;
        user.IsActive = true;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return (false, string.Join(", ", update.Errors.Select(e => e.Description)));

        return (true, "Email verified! You can now sign in and start posting blogs.");
    }

    public async Task<(bool Success, string Message)> ResendOtpAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required");

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
            return (false, "Account not found");

        if (user.EmailConfirmed)
            return (false, "Email is already verified");

        if (!_otpService.CanResend(email))
            return (false, "Please wait a minute before requesting a new code.");

        await _otpService.SendRegistrationOtpAsync(user.Email!, user.FullName);
        return (true, "A new verification code has been sent to your email.");
    }
}
