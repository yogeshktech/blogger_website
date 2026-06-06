using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class SuperAdminController : ControllerBase
{
    private readonly IBusinessLayer _businessLayer;

    public SuperAdminController(IBusinessLayer businessLayer)
    {
        _businessLayer = businessLayer;
    }

    [HttpPost("bloggers")]
    public Task<IActionResult> CreateBlogger([FromForm] IFormCollection form) =>
        _businessLayer.CreateBlogger(form, User);

    [HttpGet("bloggers")]
    public Task<IActionResult> GetBloggers() =>
        _businessLayer.GetAllBloggers(User);

    [HttpGet("bloggers/pending")]
    public Task<IActionResult> GetPendingBloggers() =>
        _businessLayer.GetPendingBloggers(User);

    [HttpPut("bloggers/{userId}/approve")]
    public Task<IActionResult> ApproveBlogger(string userId) =>
        _businessLayer.ApproveBlogger(userId, User);

    [HttpDelete("bloggers/{userId}/reject")]
    public Task<IActionResult> RejectBlogger(string userId) =>
        _businessLayer.RejectBlogger(userId, User);

    [HttpPut("bloggers/{userId}/status")]
    public Task<IActionResult> ToggleBloggerStatus(string userId, [FromQuery] bool isActive) =>
        _businessLayer.ToggleBloggerStatus(userId, isActive, User);

    [HttpGet("categories")]
    public Task<IActionResult> GetCategories() =>
        _businessLayer.GetAllCategoriesAdmin(User);

    [HttpPost("categories")]
    public Task<IActionResult> CreateCategory([FromForm] IFormCollection form)
    {
        int? parentId = int.TryParse(form["ParentId"], out var pid) ? pid : null;
        return _businessLayer.CreateCategory(form["Name"].ToString(), parentId, User);
    }

    [HttpPut("categories/{id:int}")]
    public Task<IActionResult> UpdateCategory(int id, [FromForm] IFormCollection form)
    {
        var name = form["Name"].ToString();
        int? parentId = int.TryParse(form["ParentId"], out var pid) ? pid : null;
        var isActive = bool.TryParse(form["IsActive"], out var active) && active;
        return _businessLayer.UpdateCategory(id, name, parentId, isActive, User);
    }

    [HttpDelete("categories/{id:int}")]
    public Task<IActionResult> DeleteCategory(int id) =>
        _businessLayer.DeleteCategory(id, User);

    [HttpGet("comments")]
    public Task<IActionResult> GetAllComments() =>
        _businessLayer.GetAllComments(User);
}
