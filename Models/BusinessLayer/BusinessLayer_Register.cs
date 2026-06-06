using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.Constants;
using Blogger_website.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> RegisterAdmin(IFormCollection form);
    Task<IActionResult> VerifyRegistrationOtp(IFormCollection form);
    Task<IActionResult> ResendRegistrationOtp(IFormCollection form);
    Task<IActionResult> GetPendingBloggers(ClaimsPrincipal user);
    Task<IActionResult> ApproveBlogger(string userId, ClaimsPrincipal user);
    Task<IActionResult> RejectBlogger(string userId, ClaimsPrincipal user);
}

public partial class BusinessLayer
{
    public async Task<IActionResult> RegisterAdmin(IFormCollection form)
    {
        var email = form["Email"].ToString().Trim();
        var password = form["Password"].ToString();
        var fullName = form["FullName"].ToString().Trim();

        try
        {
            var result = await _registrationService.RegisterAsync(email, password, fullName);
            if (!result.Success)
                return new BadRequestObjectResult(new { Success = false, Message = result.Message });

            return new OkObjectResult(new
            {
                Success = true,
                Message = result.Message,
                RequiresOtp = result.RequiresOtp,
                Data = new { Email = result.Email, IsActive = false, EmailConfirmed = false }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> VerifyRegistrationOtp(IFormCollection form)
    {
        var email = form["Email"].ToString().Trim();
        var otp = form["Otp"].ToString().Trim();

        try
        {
            var (success, message) = await _registrationService.VerifyOtpAsync(email, otp);
            if (!success)
                return new BadRequestObjectResult(new { Success = false, Message = message });

            return new OkObjectResult(new
            {
                Success = true,
                Message = message,
                Data = new { Email = email, EmailConfirmed = true }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> ResendRegistrationOtp(IFormCollection form)
    {
        var email = form["Email"].ToString().Trim();

        try
        {
            var (success, message) = await _registrationService.ResendOtpAsync(email);
            if (!success)
                return new BadRequestObjectResult(new { Success = false, Message = message });

            return new OkObjectResult(new { Success = true, Message = message });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }
    private async Task<IActionResult?> EnsureApprovedBlogger(ClaimsPrincipal user)
    {
        if (IsSuperAdmin(user)) return null;

        if (!user.IsInRole(AppRoles.Blogger))
            return new ForbidResult();

        var appUser = await _userManager.GetUserAsync(user);
        if (appUser == null || !appUser.IsActive)
        {
            return new ObjectResult(new
            {
                Success = false,
                Message = "Your account is pending SuperAdmin approval. You cannot manage blogs yet."
            }) { StatusCode = 403 };
        }

        return null;
    }

    public async Task<IActionResult> GetPendingBloggers(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var pending = await _databaseLayer.GetPendingBloggersAsync();
            return new OkObjectResult(new { Success = true, Data = pending });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> ApproveBlogger(string userId, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var target = await _userManager.FindByIdAsync(userId);
            if (target == null)
                return new NotFoundObjectResult(new { Success = false, Message = "User not found" });

            if (!await _userManager.IsInRoleAsync(target, AppRoles.Blogger))
                return new BadRequestObjectResult(new { Success = false, Message = "User is not an admin/blogger" });

            if (await _userManager.IsInRoleAsync(target, AppRoles.SuperAdmin))
                return new BadRequestObjectResult(new { Success = false, Message = "Cannot modify SuperAdmin" });

            await _databaseLayer.SetUserActiveStatusAsync(userId, true);

            var loginUrl = BuildLoginUrl();
            await _emailNotificationService.SendAccountApprovedAsync(
                target.Email!, target.FullName, loginUrl);

            return new OkObjectResult(new            {
                Success = true,
                Message = "Admin approved successfully. They can now login and post blogs.",
                Data = new { target.Id, target.Email, target.FullName, IsActive = true }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> RejectBlogger(string userId, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var target = await _userManager.FindByIdAsync(userId);
            if (target == null)
                return new NotFoundObjectResult(new { Success = false, Message = "User not found" });

            if (await _userManager.IsInRoleAsync(target, AppRoles.SuperAdmin))
                return new BadRequestObjectResult(new { Success = false, Message = "Cannot reject SuperAdmin" });

            var result = await _userManager.DeleteAsync(target);
            if (!result.Succeeded)
            {
                return new BadRequestObjectResult(new
                {
                    Success = false,
                    Message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return new OkObjectResult(new { Success = true, Message = "Registration rejected and account removed" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    private string BuildLoginUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return "/Identity/Account/Login";

        return $"{request.Scheme}://{request.Host}/Identity/Account/Login";
    }
}
