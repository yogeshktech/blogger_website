using Blogger_website.Models.Constants;
using Blogger_website.Models.Entities;
using Blogger_website.Models.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> AddComment(IFormCollection form, ClaimsPrincipal? user);
    Task<IActionResult> GetCommentsByBlog(int blogId);
    Task<IActionResult> ApproveComment(int id, bool isApproved, ClaimsPrincipal user);
    Task<IActionResult> DeleteComment(int id, ClaimsPrincipal user);
    Task<IActionResult> GetAllComments(ClaimsPrincipal user);
    Task<bool> CanModerateBlogComments(int blogId, ClaimsPrincipal user);
}

public partial class BusinessLayer
{
    private async Task<bool> CanManageCommentAsync(Comment comment, ClaimsPrincipal user)
    {
        if (IsSuperAdmin(user)) return true;

        if (!user.IsInRole(AppRoles.Blogger))
            return false;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return false;

        var blog = await _databaseLayer.GetBlogByIdAsync(comment.BlogId);
        return blog != null && blog.CreatedByUserId == userId;
    }

    public async Task<bool> CanModerateBlogComments(int blogId, ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        if (IsSuperAdmin(user))
            return true;

        if (!user.IsInRole(AppRoles.Blogger))
            return false;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var blog = await _databaseLayer.GetBlogByIdAsync(blogId);
        return blog != null && blog.CreatedByUserId == userId;
    }

    public async Task<IActionResult> AddComment(IFormCollection form, ClaimsPrincipal? user)
    {
        if (!int.TryParse(form["BlogId"], out var blogId))
            return new BadRequestObjectResult(new { Success = false, Message = "Valid BlogId is required" });

        var content = form["Content"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(content))
            return new BadRequestObjectResult(new { Success = false, Message = "Comment content is required" });

        int? parentId = int.TryParse(form["ParentId"], out var pid) ? pid : null;

        try
        {
            var blog = await _databaseLayer.GetBlogByIdAsync(blogId);
            if (blog == null || !blog.IsPublished)
                return new NotFoundObjectResult(new { Success = false, Message = "Blog not found" });

            if (parentId.HasValue)
            {
                var parent = await _databaseLayer.GetCommentByIdAsync(parentId.Value);
                if (parent == null || parent.BlogId != blogId)
                    return new BadRequestObjectResult(new { Success = false, Message = "Invalid reply target" });
            }

            string authorName;
            string? authorEmail = null;
            string? userId = null;

            if (user?.Identity?.IsAuthenticated == true)
            {
                userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var appUser = await _userManager.FindByIdAsync(userId!);
                authorName = appUser?.FullName ?? appUser?.UserName ?? "User";
                authorEmail = appUser?.Email;
            }
            else
            {
                authorName = form["AuthorName"].ToString().Trim();
                authorEmail = form["AuthorEmail"].ToString().Trim();

                if (string.IsNullOrWhiteSpace(authorName))
                    return new BadRequestObjectResult(new { Success = false, Message = "Author name is required" });
            }

            var comment = new Comment
            {
                BlogId = blogId,
                ParentId = parentId,
                UserId = userId,
                AuthorName = authorName,
                AuthorEmail = string.IsNullOrWhiteSpace(authorEmail) ? null : authorEmail,
                Content = content,
                IsApproved = true
            };

            var id = await _databaseLayer.CreateCommentAsync(comment);
            comment.Id = id;
            comment.CreatedAt = DateTime.UtcNow;

            return new OkObjectResult(new
            {
                Success = true,
                Message = parentId.HasValue ? "Reply posted" : "Comment added",
                Data = comment
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetCommentsByBlog(int blogId)
    {
        try
        {
            var comments = await _databaseLayer.GetCommentsByBlogIdAsync(blogId);
            var threads = CommentHelper.BuildThread(comments);
            return new OkObjectResult(new { Success = true, Data = threads, Total = comments.Count });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> ApproveComment(int id, bool isApproved, ClaimsPrincipal user)
    {
        if (!IsSuperAdmin(user))
            return new ForbidResult();

        try
        {
            var comment = await _databaseLayer.GetCommentByIdAsync(id);
            if (comment == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Comment not found" });

            await _databaseLayer.UpdateCommentApprovalAsync(id, isApproved);
            return new OkObjectResult(new { Success = true, Message = isApproved ? "Comment approved" : "Comment hidden" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> DeleteComment(int id, ClaimsPrincipal user)
    {
        try
        {
            var comment = await _databaseLayer.GetCommentByIdAsync(id);
            if (comment == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Comment not found" });

            if (!await CanManageCommentAsync(comment, user))
                return new ForbidResult();

            await _databaseLayer.DeleteCommentAsync(id);
            return new OkObjectResult(new { Success = true, Message = "Comment deleted" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetAllComments(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin) && !user.IsInRole(AppRoles.Blogger))
            return new ForbidResult();

        try
        {
            string? ownerId = null;
            if (!IsSuperAdmin(user))
            {
                ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(ownerId))
                    return new ForbidResult();
            }

            var comments = await _databaseLayer.GetAllCommentsAsync(ownerId);
            return new OkObjectResult(new { Success = true, Data = comments });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }
}
