// Licensed to the .NET Foundation under one or more agreements.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Blogger_website.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogger_website.Areas.Identity.Pages.Account;

public class VerifyEmailOtpModel : PageModel
{
    private readonly IAdminRegistrationService _registrationService;

    public VerifyEmailOtpModel(IAdminRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ReturnUrl { get; set; }

    public string InfoMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be 6 digits")]
        [Display(Name = "Verification Code")]
        public string Otp { get; set; }
    }

    public IActionResult OnGet(string email = null, string returnUrl = null, string info = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToPage("./Register");

        ReturnUrl = returnUrl ?? Url.Content("~/");
        InfoMessage = info ?? "We sent a 6-digit code to your email. Enter it below to verify.";
        Input = new InputModel { Email = email };
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync(string returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid)
            return Page();

        var (success, message) = await _registrationService.VerifyOtpAsync(Input.Email, Input.Otp);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            InfoMessage = "Enter the 6-digit code from your email.";
            return Page();
        }

        return RedirectToPage("./Login", new { returnUrl, pendingApproval = true, emailVerified = true });
    }

    public async Task<IActionResult> OnPostResendAsync(string returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (string.IsNullOrWhiteSpace(Input?.Email))
        {
            ModelState.AddModelError(string.Empty, "Email is required to resend OTP.");
            return Page();
        }

        var (success, message) = await _registrationService.ResendOtpAsync(Input.Email);
        InfoMessage = success ? message : message;
        if (!success)
            ModelState.AddModelError(string.Empty, message);

        Input ??= new InputModel();
        Input.Otp = string.Empty;
        return Page();
    }
}
