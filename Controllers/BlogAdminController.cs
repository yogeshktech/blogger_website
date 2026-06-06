using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[ApiController]
[Route("api/admin/blogs")]
[Authorize(Roles = $"{AppRoles.Blogger},{AppRoles.SuperAdmin}")]
public class BlogAdminController : ControllerBase
{
    private readonly IBusinessLayer _businessLayer;

    public BlogAdminController(IBusinessLayer businessLayer)
    {
        _businessLayer = businessLayer;
    }

    [HttpGet]
    public Task<IActionResult> GetMyBlogs() =>
        _businessLayer.GetMyBlogs(User);

    [HttpGet("{id:int}")]
    public Task<IActionResult> GetById(int id) =>
        _businessLayer.GetBlogById(id, User);

    [HttpPost]
    public Task<IActionResult> Create([FromForm] IFormCollection form) =>
        _businessLayer.CreateBlog(form, User);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, [FromForm] IFormCollection form) =>
        _businessLayer.UpdateBlog(id, form, User);

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id) =>
        _businessLayer.DeleteBlog(id, User);
}
