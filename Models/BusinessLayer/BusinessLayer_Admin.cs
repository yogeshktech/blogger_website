using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> CreateAdmin(IFormCollection form, System.Security.Claims.ClaimsPrincipal user);
}

public partial class BusinessLayer
{
    public Task<IActionResult> CreateAdmin(IFormCollection form, System.Security.Claims.ClaimsPrincipal user)
    {
        return CreateBlogger(form, user);
    }
}
