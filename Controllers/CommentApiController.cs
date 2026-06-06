using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentApiController : ControllerBase
{
    private readonly IBusinessLayer _businessLayer;

    public CommentApiController(IBusinessLayer businessLayer)
    {
        _businessLayer = businessLayer;
    }

    [HttpGet("blog/{blogId:int}")]
    public Task<IActionResult> GetByBlog(int blogId) =>
        _businessLayer.GetCommentsByBlog(blogId);

    [HttpPost]
    [AllowAnonymous]
    public Task<IActionResult> Add([FromForm] IFormCollection form) =>
        _businessLayer.AddComment(form, User);

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Blogger},{AppRoles.SuperAdmin}")]
    public Task<IActionResult> GetAll() =>
        _businessLayer.GetAllComments(User);

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public Task<IActionResult> Approve(int id, [FromQuery] bool isApproved = true) =>
        _businessLayer.ApproveComment(id, isApproved, User);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{AppRoles.Blogger},{AppRoles.SuperAdmin}")]
    public Task<IActionResult> Delete(int id) =>
        _businessLayer.DeleteComment(id, User);
}
