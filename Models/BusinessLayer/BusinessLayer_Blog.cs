using Blogger_website.Models.Constants;
using Blogger_website.Models.Entities;
using Blogger_website.Models.Helpers;
using CareerCracker.S3Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> GetPublishedBlogs(int? categoryId, int? labelId = null);
    Task<IActionResult> GetBlogBySlug(string slug);
    Task<IActionResult> GetMyBlogs(ClaimsPrincipal user);
    Task<IActionResult> GetBlogById(int id, ClaimsPrincipal user);
    Task<IActionResult> CreateBlog(IFormCollection form, ClaimsPrincipal user);
    Task<IActionResult> UpdateBlog(int id, IFormCollection form, ClaimsPrincipal user);
    Task<IActionResult> DeleteBlog(int id, ClaimsPrincipal user);
    Task<IActionResult> GetCategories();
    Task<IActionResult> GetCategoryChildren(int? parentId);
    Task<IActionResult> GetLabels();
}

public partial class BusinessLayer
{
    private bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.SuperAdmin);

    private Task<bool> CanManageBlog(Blog blog, ClaimsPrincipal user)
    {
        if (IsSuperAdmin(user)) return Task.FromResult(true);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var canManage = user.IsInRole(AppRoles.Blogger) && blog.CreatedByUserId == userId;
        return Task.FromResult(canManage);
    }

    public async Task<IActionResult> GetPublishedBlogs(int? categoryId, int? labelId = null)
    {
        try
        {
            var blogs = await _databaseLayer.GetPublishedBlogsAsync(categoryId, labelId);
            return new OkObjectResult(new { Success = true, Data = blogs });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetBlogBySlug(string slug)
    {
        try
        {
            var blog = await _databaseLayer.GetBlogBySlugAsync(slug);
            if (blog == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Blog not found" });

            var comments = await _databaseLayer.GetCommentsByBlogIdAsync(blog.Id);
            return new OkObjectResult(new { Success = true, Data = new { Blog = blog, Comments = comments } });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetMyBlogs(ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return new UnauthorizedObjectResult(new { Success = false, Message = "Login required" });

        var approvalCheck = await EnsureApprovedBlogger(user);
        if (approvalCheck != null) return approvalCheck;

        try
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            List<Blog> blogs;

            if (IsSuperAdmin(user))
                blogs = await _databaseLayer.GetAllBlogsAsync();
            else if (user.IsInRole(AppRoles.Blogger))
                blogs = await _databaseLayer.GetBlogsByUserIdAsync(userId);
            else
                return new ForbidResult();

            return new OkObjectResult(new { Success = true, Data = blogs });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetBlogById(int id, ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return new UnauthorizedObjectResult(new { Success = false, Message = "Login required" });

        try
        {
            var blog = await _databaseLayer.GetBlogByIdAsync(id);
            if (blog == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Blog not found" });

            if (!await CanManageBlog(blog, user))
                return new ForbidResult();

            return new OkObjectResult(new { Success = true, Data = blog });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> CreateBlog(IFormCollection form, ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return new UnauthorizedObjectResult(new { Success = false, Message = "Login required" });

        var approvalCheck = await EnsureApprovedBlogger(user);
        if (approvalCheck != null) return approvalCheck;

        if (!user.IsInRole(AppRoles.Blogger) && !IsSuperAdmin(user))
            return new ForbidResult();

        var title = form["Title"].ToString().Trim();
        var content = form["Content"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return new BadRequestObjectResult(new { Success = false, Message = "Title and Content are required" });

        try
        {
            var slug = SlugHelper.Generate(title);
            var suffix = 1;
            while (await _databaseLayer.SlugExistsAsync(slug))
                slug = $"{SlugHelper.Generate(title)}-{suffix++}";

            string? imageUrl = null;
            if (form.Files["FeaturedImage"] is { Length: > 0 } file)
                imageUrl = await S3StorageHelper.UploadFileAsync(file, "blogs");

            int? categoryId = int.TryParse(form["CategoryId"], out var catId) ? catId : null;
            bool isPublished = bool.TryParse(form["IsPublished"], out var pub) && pub;

            var blog = new Blog
            {
                Title = title,
                Slug = slug,
                ShortDescription = form["ShortDescription"].ToString().Trim(),
                Content = content,
                FeaturedImage = imageUrl,
                CategoryId = categoryId,
                CreatedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                IsPublished = isPublished
            };

            var id = await _databaseLayer.CreateBlogAsync(blog);
            blog.Id = id;
            await _databaseLayer.SetBlogLabelsAsync(id, ParseLabelIds(form));

            return new OkObjectResult(new { Success = true, Message = "Blog created", Data = blog });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> UpdateBlog(int id, IFormCollection form, ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return new UnauthorizedObjectResult(new { Success = false, Message = "Login required" });

        var approvalCheck = await EnsureApprovedBlogger(user);
        if (approvalCheck != null) return approvalCheck;

        try
        {
            var existing = await _databaseLayer.GetBlogByIdAsync(id);
            if (existing == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Blog not found" });

            if (!await CanManageBlog(existing, user))
                return new ForbidResult();

            var title = form["Title"].ToString().Trim();
            var content = form["Content"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                return new BadRequestObjectResult(new { Success = false, Message = "Title and Content are required" });

            var slug = existing.Slug;
            if (!string.Equals(title, existing.Title, StringComparison.OrdinalIgnoreCase))
            {
                slug = SlugHelper.Generate(title);
                var suffix = 1;
                while (await _databaseLayer.SlugExistsAsync(slug, id))
                    slug = $"{SlugHelper.Generate(title)}-{suffix++}";
            }

            if (form.Files["FeaturedImage"] is { Length: > 0 } file)
            {
                if (!string.IsNullOrWhiteSpace(existing.FeaturedImage))
                    await S3StorageHelper.DeleteStoredMediaAsync(existing.FeaturedImage);
                existing.FeaturedImage = await S3StorageHelper.UploadFileAsync(file, "blogs");
            }

            existing.Title = title;
            existing.Slug = slug;
            existing.ShortDescription = form["ShortDescription"].ToString().Trim();
            existing.Content = content;
            existing.CategoryId = int.TryParse(form["CategoryId"], out var catId) ? catId : null;
            existing.IsPublished = bool.TryParse(form["IsPublished"], out var pub) && pub;

            await _databaseLayer.UpdateBlogAsync(existing);
            await _databaseLayer.SetBlogLabelsAsync(id, ParseLabelIds(form));

            return new OkObjectResult(new { Success = true, Message = "Blog updated", Data = existing });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> DeleteBlog(int id, ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return new UnauthorizedObjectResult(new { Success = false, Message = "Login required" });

        var approvalCheck = await EnsureApprovedBlogger(user);
        if (approvalCheck != null) return approvalCheck;

        try
        {
            var existing = await _databaseLayer.GetBlogByIdAsync(id);
            if (existing == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Blog not found" });

            if (!await CanManageBlog(existing, user))
                return new ForbidResult();

            if (!string.IsNullOrWhiteSpace(existing.FeaturedImage))
                await S3StorageHelper.DeleteStoredMediaAsync(existing.FeaturedImage);

            await _databaseLayer.DeleteBlogAsync(id);

            return new OkObjectResult(new { Success = true, Message = "Blog deleted" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var tree = await _databaseLayer.GetCategoriesAsync();
            var flat = await _databaseLayer.GetCategoriesFlatAsync();
            return new OkObjectResult(new { Success = true, Data = tree, Flat = flat });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetCategoryChildren(int? parentId)
    {
        try
        {
            var children = await _databaseLayer.GetCategoryChildrenAsync(parentId);
            return new OkObjectResult(new { Success = true, Data = children });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetLabels()
    {
        try
        {
            var labels = await _databaseLayer.GetLabelsAsync();
            return new OkObjectResult(new { Success = true, Data = labels });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    private static IEnumerable<int> ParseLabelIds(IFormCollection form)
    {
        var ids = new List<int>();
        foreach (var key in form.Keys.Where(k => k == "LabelIds"))
        {
            foreach (var val in form[key])
            {
                if (int.TryParse(val, out var id))
                    ids.Add(id);
            }
        }
        return ids;
    }
}
