using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> CreateBlogger(IFormCollection form, ClaimsPrincipal user);
    Task<IActionResult> GetAllBloggers(ClaimsPrincipal user);
    Task<IActionResult> ToggleBloggerStatus(string userId, bool isActive, ClaimsPrincipal user);
    Task<IActionResult> CreateCategory(string name, int? parentId, ClaimsPrincipal user);
    Task<IActionResult> UpdateCategory(int id, string name, int? parentId, bool isActive, ClaimsPrincipal user);
    Task<IActionResult> DeleteCategory(int id, ClaimsPrincipal user);
    Task<IActionResult> GetAllCategoriesAdmin(ClaimsPrincipal user);
    Task<IActionResult> CreateLabel(string name, ClaimsPrincipal user);
    Task<IActionResult> UpdateLabel(int id, string name, bool isActive, ClaimsPrincipal user);
    Task<IActionResult> DeleteLabel(int id, ClaimsPrincipal user);
    Task<IActionResult> GetAllLabelsAdmin(ClaimsPrincipal user);
}

public partial class BusinessLayer
{
    public async Task<IActionResult> CreateBlogger(IFormCollection form, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        var email = form["Email"].ToString().Trim();
        var password = form["Password"].ToString();
        var fullName = form["FullName"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return new BadRequestObjectResult(new { Success = false, Message = "Email and Password are required" });

        try
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
                return new BadRequestObjectResult(new { Success = false, Message = "Email already exists" });

            var blogger = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(blogger, password);
            if (!result.Succeeded)
            {
                return new BadRequestObjectResult(new
                {
                    Success = false,
                    Message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            await _userManager.AddToRoleAsync(blogger, AppRoles.Blogger);

            return new OkObjectResult(new
            {
                Success = true,
                Message = "Blogger created and approved successfully",
                Data = new { blogger.Id, blogger.Email, blogger.FullName, IsActive = true }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetAllBloggers(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var bloggers = await _databaseLayer.GetAllBloggersAsync();
            return new OkObjectResult(new { Success = true, Data = bloggers });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> ToggleBloggerStatus(string userId, bool isActive, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var target = await _userManager.FindByIdAsync(userId);
            if (target == null)
                return new NotFoundObjectResult(new { Success = false, Message = "User not found" });

            if (await _userManager.IsInRoleAsync(target, AppRoles.SuperAdmin))
                return new BadRequestObjectResult(new { Success = false, Message = "Cannot modify SuperAdmin" });

            await _databaseLayer.SetUserActiveStatusAsync(userId, isActive);
            return new OkObjectResult(new { Success = true, Message = isActive ? "Blogger activated" : "Blogger deactivated" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> CreateCategory(string name, int? parentId, ClaimsPrincipal user)
    {
        var approvalCheck = await EnsureApprovedBlogger(user);
        if (approvalCheck != null)
            return approvalCheck;

        if (string.IsNullOrWhiteSpace(name))
            return new BadRequestObjectResult(new { Success = false, Message = "Category name is required" });

        try
        {
            var id = await _databaseLayer.CreateCategoryAsync(name.Trim(), parentId);
            return new OkObjectResult(new { Success = true, Message = "Category created", Data = new { Id = id, Name = name, ParentId = parentId } });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> UpdateCategory(int id, string name, int? parentId, bool isActive, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var updated = await _databaseLayer.UpdateCategoryAsync(id, name, parentId, isActive);
            if (!updated)
                return new NotFoundObjectResult(new { Success = false, Message = "Category not found" });

            return new OkObjectResult(new { Success = true, Message = "Category updated" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> DeleteCategory(int id, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var deleted = await _databaseLayer.DeleteCategoryAsync(id);
            if (!deleted)
                return new NotFoundObjectResult(new { Success = false, Message = "Category not found" });

            return new OkObjectResult(new { Success = true, Message = "Category deleted" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetAllCategoriesAdmin(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var categories = await _databaseLayer.GetCategoriesAsync(activeOnly: false);
            return new OkObjectResult(new { Success = true, Data = categories });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> CreateLabel(string name, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(name))
            return new BadRequestObjectResult(new { Success = false, Message = "Label name is required" });

        try
        {
            var id = await _databaseLayer.CreateLabelAsync(name.Trim());
            return new OkObjectResult(new { Success = true, Message = "Label created", Data = new { Id = id, Name = name } });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> UpdateLabel(int id, string name, bool isActive, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var updated = await _databaseLayer.UpdateLabelAsync(id, name, isActive);
            if (!updated)
                return new NotFoundObjectResult(new { Success = false, Message = "Label not found" });

            return new OkObjectResult(new { Success = true, Message = "Label updated" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> DeleteLabel(int id, ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            await _databaseLayer.DeleteLabelAsync(id);
            return new OkObjectResult(new { Success = true, Message = "Label deleted" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetAllLabelsAdmin(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AppRoles.SuperAdmin))
            return new ForbidResult();

        try
        {
            var labels = await _databaseLayer.GetLabelsAsync(activeOnly: false);
            return new OkObjectResult(new { Success = true, Data = labels });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }
}
