using Blogger_website.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[ApiController]
[Route("api/blogs")]
public class BlogApiController : ControllerBase
{
    private readonly IBusinessLayer _businessLayer;

    public BlogApiController(IBusinessLayer businessLayer)
    {
        _businessLayer = businessLayer;
    }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] int? categoryId, [FromQuery] int? labelId) =>
        _businessLayer.GetPublishedBlogs(categoryId, labelId);

    [HttpGet("{slug}")]
    public Task<IActionResult> GetBySlug(string slug) =>
        _businessLayer.GetBlogBySlug(slug);

    [HttpGet("categories")]
    public Task<IActionResult> GetCategories() =>
        _businessLayer.GetCategories();

    [HttpGet("categories/children")]
    public Task<IActionResult> GetCategoryChildren([FromQuery] int? parentId) =>
        _businessLayer.GetCategoryChildren(parentId);

    [HttpGet("labels")]
    public Task<IActionResult> GetLabels() =>
        _businessLayer.GetLabels();
}
