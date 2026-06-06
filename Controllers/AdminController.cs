using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IBusinessLayer _businessLayer;

    public AdminController(IBusinessLayer businessLayer)
    {
        _businessLayer = businessLayer;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAdmin(IFormCollection form)
    {
        if (form == null)
            return BadRequest(new { Success = false, Message = "Please fill required fields" });

        return await _businessLayer.RegisterAdmin(form);
    }

    [HttpPost("register/verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyRegistrationOtp(IFormCollection form)
    {
        if (form == null)
            return BadRequest(new { Success = false, Message = "Email and OTP are required" });

        return await _businessLayer.VerifyRegistrationOtp(form);
    }

    [HttpPost("register/resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendRegistrationOtp(IFormCollection form)
    {
        if (form == null)
            return BadRequest(new { Success = false, Message = "Email is required" });

        return await _businessLayer.ResendRegistrationOtp(form);
    }

    [HttpPost("create-blogger")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> CreateBlogger(IFormCollection form)
    {
        if (form == null)
            return BadRequest(new { Success = false, Message = "Please fill required fields" });

        return await _businessLayer.CreateAdmin(form, User);
    }
}
