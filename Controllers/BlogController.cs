using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Models.Helpers;
using Blogger_website.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

public class BlogController : Controller
{
    private readonly IBusinessLayer _businessLayer;
    private readonly IDatabaseLayer _databaseLayer;
    private readonly UserManager<ApplicationUser> _userManager;

    public BlogController(IBusinessLayer businessLayer, IDatabaseLayer databaseLayer, UserManager<ApplicationUser> userManager)
    {
        _businessLayer = businessLayer;
        _databaseLayer = databaseLayer;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int? categoryId, int? labelId)
    {
        var blogs = await _databaseLayer.GetPublishedBlogsAsync(categoryId, labelId);
        await _databaseLayer.AttachBlogLikesAsync(blogs, BlogEngagementHelper.GetLikeKey(HttpContext));

        var tree = await _databaseLayer.GetCategoriesAsync();
        var flat = await _databaseLayer.GetCategoriesFlatAsync();

        var model = new BlogListViewModel
        {
            Blogs = blogs,
            CategoryTree = tree,
            CategoriesFlat = flat,
            Labels = await _databaseLayer.GetLabelsAsync(),
            SelectedCategoryId = categoryId,
            SelectedLabelId = labelId,
            CategoryBrowse = CategoryBrowseHelper.Build(tree, flat, categoryId, "Blog", "Index", labelId)
        };
        ViewData["Title"] = "Blogs";
        return View(model);
    }

    public async Task<IActionResult> Details(string slug)
    {
        var blog = await _databaseLayer.GetBlogBySlugAsync(slug);
        if (blog == null) return NotFound();

        await _databaseLayer.AttachBlogLikesAsync(new[] { blog }, BlogEngagementHelper.GetLikeKey(HttpContext));

        var flatComments = await _databaseLayer.GetCommentsByBlogIdAsync(blog.Id);
        var canModerate = await _businessLayer.CanModerateBlogComments(blog.Id, User);

        string? displayName = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            displayName = user?.FullName ?? user?.Email ?? user?.UserName;
        }

        var model = new BlogDetailViewModel
        {
            Blog = blog,
            Comments = flatComments,
            CommentThreads = CommentHelper.BuildThread(flatComments),
            TotalCommentCount = CommentHelper.CountAll(flatComments),
            CanModerateComments = canModerate,
            CurrentUserDisplayName = displayName
        };
        ViewData["Title"] = blog.Title;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(string slug, int blogId, string content, string? authorName, string? authorEmail, int? parentId)
    {
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["BlogId"] = blogId.ToString(),
            ["Content"] = content,
            ["AuthorName"] = authorName ?? "",
            ["AuthorEmail"] = authorEmail ?? "",
            ["ParentId"] = parentId?.ToString() ?? ""
        });

        var result = await _businessLayer.AddComment(form, User);
        TempData["CommentMessage"] = result is OkObjectResult
            ? (parentId.HasValue ? "Reply posted successfully!" : "Comment posted successfully!")
            : "Failed to post comment.";

        return RedirectToAction(nameof(Details), new { slug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Blogger},{AppRoles.SuperAdmin}")]
    public async Task<IActionResult> DeleteComment(int id, string slug)
    {
        var result = await _businessLayer.DeleteComment(id, User);
        TempData["CommentMessage"] = result is OkObjectResult
            ? "Comment deleted."
            : "You cannot delete this comment.";

        return RedirectToAction(nameof(Details), new { slug });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleLike(int blogId)
    {
        var blog = await _databaseLayer.GetBlogByIdAsync(blogId);
        if (blog == null || !blog.IsPublished)
            return NotFound(new { success = false });

        var (liked, count) = await _databaseLayer.ToggleBlogLikeAsync(blogId, BlogEngagementHelper.GetLikeKey(HttpContext));
        return Json(new { success = true, liked, count });
    }
}
