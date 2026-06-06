using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Models.Entities;
using Blogger_website.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

[Authorize(Roles = $"{AppRoles.Blogger},{AppRoles.SuperAdmin}")]
public class AdminPanelController : Controller
{
    private readonly IBusinessLayer _businessLayer;
    private readonly IDatabaseLayer _databaseLayer;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminPanelController(
        IBusinessLayer businessLayer,
        IDatabaseLayer databaseLayer,
        UserManager<ApplicationUser> userManager)
    {
        _businessLayer = businessLayer;
        _databaseLayer = databaseLayer;
        _userManager = userManager;
    }

    private bool IsSuperAdmin => User.IsInRole(AppRoles.SuperAdmin);

    private async Task<IActionResult?> EnsureApprovedAsync()
    {
        if (IsSuperAdmin) return null;

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
            return View("PendingApproval");

        return null;
    }

    public async Task<IActionResult> Index()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var user = await _userManager.GetUserAsync(User);
        List<Blog> blogs;

        if (IsSuperAdmin)
            blogs = await _databaseLayer.GetAllBlogsAsync();
        else
            blogs = await _databaseLayer.GetBlogsByUserIdAsync(user!.Id);

        var pendingAdmins = IsSuperAdmin
            ? (await _databaseLayer.GetPendingBloggersAsync()).Count
            : 0;

        var comments = IsSuperAdmin
            ? await _databaseLayer.GetAllCommentsAsync()
            : [];

        var model = new AdminDashboardViewModel
        {
            IsSuperAdmin = IsSuperAdmin,
            UserName = user?.FullName ?? user?.Email ?? "Admin",
            TotalBlogs = blogs.Count,
            PublishedBlogs = blogs.Count(b => b.IsPublished),
            DraftBlogs = blogs.Count(b => !b.IsPublished),
            PendingAdmins = pendingAdmins,
            TotalComments = comments.Count,
            RecentBlogs = blogs.Take(5).ToList()
        };

        ViewData["Title"] = "Dashboard";
        return View(model);
    }

    public async Task<IActionResult> Blogs()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var user = await _userManager.GetUserAsync(User);
        var blogs = IsSuperAdmin
            ? await _databaseLayer.GetAllBlogsAsync()
            : await _databaseLayer.GetBlogsByUserIdAsync(user!.Id);

        ViewData["Title"] = "My Blogs";
        return View(new AdminBlogListViewModel { Blogs = blogs, IsSuperAdmin = IsSuperAdmin });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        ViewData["Title"] = "New Blog";
        return View(await LoadBlogFormViewModelAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlogFormViewModel model, IFormFile? featuredImage)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var form = BuildBlogForm(model, featuredImage);
        var result = await _businessLayer.CreateBlog(form, User);

        if (result is OkObjectResult)
        {
            TempData["Success"] = "Blog created successfully!";
            return RedirectToAction(nameof(Blogs));
        }

        if (result is ObjectResult obj && obj.Value != null)
        {
            dynamic val = obj.Value;
            TempData["Error"] = (string?)val.Message ?? "Failed to create blog.";
        }

        await MergeBlogFormListsAsync(model);
        ViewData["Title"] = "New Blog";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var blog = await _databaseLayer.GetBlogByIdAsync(id);
        if (blog == null) return NotFound();

        if (!IsSuperAdmin && blog.CreatedByUserId != _userManager.GetUserId(User))
            return Forbid();

        ViewData["Title"] = "Edit Blog";
        var model = await LoadBlogFormViewModelAsync();
        model.Id = blog.Id;
        model.Title = blog.Title;
        model.ShortDescription = blog.ShortDescription;
        model.Content = blog.Content;
        model.FeaturedImage = blog.FeaturedImage;
        model.CategoryId = blog.CategoryId;
        model.IsPublished = blog.IsPublished;
        model.SelectedLabelIds = blog.Labels.Select(l => l.Id).ToList();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BlogFormViewModel model, IFormFile? featuredImage)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var form = BuildBlogForm(model, featuredImage);
        var result = await _businessLayer.UpdateBlog(id, form, User);

        if (result is OkObjectResult)
        {
            TempData["Success"] = "Blog updated successfully!";
            return RedirectToAction(nameof(Blogs));
        }

        if (result is ObjectResult obj && obj.Value != null)
        {
            dynamic val = obj.Value;
            TempData["Error"] = (string?)val.Message ?? "Failed to update blog.";
        }

        await MergeBlogFormListsAsync(model);
        ViewData["Title"] = "Edit Blog";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.DeleteBlog(id, User);
        TempData[result is OkObjectResult ? "Success" : "Error"] =
            result is OkObjectResult ? "Blog deleted." : "Could not delete blog.";
        return RedirectToAction(nameof(Blogs));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Pending()
    {
        ViewData["Title"] = "Pending Admins";
        return View(new PendingAdminsViewModel
        {
            Pending = await _databaseLayer.GetPendingBloggersAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Approve(string userId)
    {
        var result = await _businessLayer.ApproveBlogger(userId, User);
        TempData[result is OkObjectResult ? "Success" : "Error"] =
            result is OkObjectResult ? "Admin approved!" : "Approval failed.";
        return RedirectToAction(nameof(Pending));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Reject(string userId)
    {
        var result = await _businessLayer.RejectBlogger(userId, User);
        TempData[result is OkObjectResult ? "Success" : "Error"] =
            result is OkObjectResult ? "Registration rejected." : "Rejection failed.";
        return RedirectToAction(nameof(Pending));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Bloggers()
    {
        ViewData["Title"] = "All Bloggers";
        return View(new BloggersViewModel
        {
            Bloggers = await _databaseLayer.GetAllBloggersAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> ToggleStatus(string userId, bool isActive)
    {
        await _businessLayer.ToggleBloggerStatus(userId, isActive, User);
        TempData["Success"] = isActive ? "Blogger activated." : "Blogger deactivated.";
        return RedirectToAction(nameof(Bloggers));
    }

    public async Task<IActionResult> Categories()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        ViewData["Title"] = "Categories";
        return View(new CategoriesAdminViewModel
        {
            IsSuperAdmin = IsSuperAdmin,
            CategoryTree = await _databaseLayer.GetCategoriesAsync(activeOnly: false),
            CategoriesFlat = await _databaseLayer.GetCategoriesFlatAsync(activeOnly: false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name, int? parentId)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.CreateCategory(name, parentId, User);
        if (result is OkObjectResult)
            TempData["Success"] = "Category added.";
        else
            TempData["Error"] = "Could not add category. Please check the name and try again.";

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> UpdateCategory(int id, string name, int? parentId, bool isActive)
    {
        await _businessLayer.UpdateCategory(id, name, parentId, isActive, User);
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _businessLayer.DeleteCategory(id, User);
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Categories));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Labels()
    {
        ViewData["Title"] = "Labels";
        return View(new LabelsAdminViewModel
        {
            Labels = await _databaseLayer.GetLabelsAsync(activeOnly: false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> AddLabel(string name)
    {
        await _businessLayer.CreateLabel(name, User);
        TempData["Success"] = "Label added.";
        return RedirectToAction(nameof(Labels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> UpdateLabel(int id, string name, bool isActive)
    {
        await _businessLayer.UpdateLabel(id, name, isActive, User);
        TempData["Success"] = "Label updated.";
        return RedirectToAction(nameof(Labels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> DeleteLabel(int id)
    {
        await _businessLayer.DeleteLabel(id, User);
        TempData["Success"] = "Label deleted.";
        return RedirectToAction(nameof(Labels));
    }

    [HttpGet]
    public async Task<IActionResult> CategoryChildren(int? parentId)
    {
        var children = await _databaseLayer.GetCategoryChildrenAsync(parentId);
        return Json(children.Select(c => new { c.Id, c.Name, c.Depth, c.LevelName, c.FullPath }));
    }

    public async Task<IActionResult> Comments()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var ownerId = IsSuperAdmin ? null : _userManager.GetUserId(User);
        ViewData["Title"] = "Comments";
        return View(new CommentsAdminViewModel
        {
            IsSuperAdmin = IsSuperAdmin,
            Comments = await _databaseLayer.GetAllCommentsAsync(ownerId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, string? returnSlug = null)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.DeleteComment(id, User);
        if (result is OkObjectResult)
        {
            TempData["Success"] = "Comment deleted.";
            if (!string.IsNullOrEmpty(returnSlug))
                return RedirectToAction("Details", "Blog", new { slug = returnSlug });
        }
        else
        {
            TempData["Error"] = "Could not delete comment.";
        }

        return RedirectToAction(nameof(Comments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> ToggleCommentApproval(int id, bool isApproved)
    {
        await _businessLayer.ApproveComment(id, isApproved, User);
        TempData["Success"] = isApproved ? "Comment approved." : "Comment hidden.";
        return RedirectToAction(nameof(Comments));
    }

    private async Task<BlogFormViewModel> LoadBlogFormViewModelAsync()
    {
        var flat = await _databaseLayer.GetCategoriesFlatAsync();
        return new BlogFormViewModel
        {
            CategoriesFlat = flat,
            RootCategories = flat.Where(c => c.ParentId == null).ToList(),
            Labels = await _databaseLayer.GetLabelsAsync()
        };
    }

    private async Task MergeBlogFormListsAsync(BlogFormViewModel model)
    {
        var loaded = await LoadBlogFormViewModelAsync();
        model.CategoriesFlat = loaded.CategoriesFlat;
        model.RootCategories = loaded.RootCategories;
        model.Labels = loaded.Labels;
    }

    private static FormCollection BuildBlogForm(BlogFormViewModel model, IFormFile? featuredImage)
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["Title"] = model.Title,
            ["ShortDescription"] = model.ShortDescription ?? "",
            ["Content"] = model.Content,
            ["CategoryId"] = model.CategoryId?.ToString() ?? "",
            ["IsPublished"] = model.IsPublished.ToString()
        };

        if (model.SelectedLabelIds.Count > 0)
            dict["LabelIds"] = new Microsoft.Extensions.Primitives.StringValues(
                model.SelectedLabelIds.Select(id => id.ToString()).ToArray());

        var files = new FormFileCollection();
        if (featuredImage != null && featuredImage.Length > 0)
            files.Add(featuredImage);

        return new FormCollection(dict, files);
    }
}
