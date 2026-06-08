using Blogger_website.Models.Constants;
using Blogger_website.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Blogger_website.Models.BusinessLayer;

public partial interface IBusinessLayer
{
    Task<IActionResult> SendVendorConnectionRequest(string toUserId, string? introMessage, ClaimsPrincipal user);
    Task<IActionResult> AcceptVendorConnectionRequest(int requestId, ClaimsPrincipal user);
    Task<IActionResult> RejectVendorConnectionRequest(int requestId, ClaimsPrincipal user);
    Task<IActionResult> SendVendorChatMessage(int threadId, string content, ClaimsPrincipal user);
    Task<IActionResult> EditVendorChatMessage(int messageId, string content, ClaimsPrincipal user);
    Task<IActionResult> HideVendorChatMessage(int messageId, ClaimsPrincipal user);
    Task<IActionResult> DeleteVendorChatMessageForEveryone(int messageId, ClaimsPrincipal user);
}

public partial class BusinessLayer
{
    private async Task<IActionResult?> EnsureApprovedVendorAsync(ClaimsPrincipal user)
    {
        if (IsSuperAdmin(user)) return null;
        return await EnsureApprovedBlogger(user);
    }

    public async Task<IActionResult> SendVendorConnectionRequest(string toUserId, string? introMessage, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var fromUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(fromUserId) || string.IsNullOrEmpty(toUserId))
            return new BadRequestObjectResult(new { Success = false, Message = "Invalid vendor selection" });

        if (fromUserId == toUserId)
            return new BadRequestObjectResult(new { Success = false, Message = "You cannot send a request to yourself" });

        try
        {
            var target = await _userManager.FindByIdAsync(toUserId);
            if (target == null || !target.IsActive || !await _userManager.IsInRoleAsync(target, AppRoles.Blogger))
                return new BadRequestObjectResult(new { Success = false, Message = "Vendor not found or not active" });

            var existing = await _databaseLayer.GetConnectionBetweenUsersAsync(fromUserId, toUserId);
            if (existing != null)
            {
                if (existing.Status == VendorConnectionStatus.Accepted)
                    return new BadRequestObjectResult(new { Success = false, Message = "You are already connected with this vendor" });
                if (existing.Status == VendorConnectionStatus.Pending)
                    return new BadRequestObjectResult(new { Success = false, Message = "A pending request already exists" });
            }

            var id = await _databaseLayer.CreateConnectionRequestAsync(fromUserId, toUserId, introMessage?.Trim());
            return new OkObjectResult(new { Success = true, Message = "Connection request sent", Data = new { Id = id } });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> AcceptVendorConnectionRequest(int requestId, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        try
        {
            var request = await _databaseLayer.GetConnectionRequestByIdAsync(requestId);
            if (request == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Request not found" });

            if (request.ToUserId != userId)
                return new ForbidResult();

            if (request.Status != VendorConnectionStatus.Pending)
                return new BadRequestObjectResult(new { Success = false, Message = "Request is no longer pending" });

            await _databaseLayer.UpdateConnectionRequestStatusAsync(requestId, VendorConnectionStatus.Accepted);
            var threadId = await _databaseLayer.CreateChatThreadAsync(requestId, request.FromUserId, request.ToUserId);

            return new OkObjectResult(new
            {
                Success = true,
                Message = "Request accepted. You can now chat.",
                Data = new { ThreadId = threadId }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> RejectVendorConnectionRequest(int requestId, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        try
        {
            var request = await _databaseLayer.GetConnectionRequestByIdAsync(requestId);
            if (request == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Request not found" });

            if (request.ToUserId != userId)
                return new ForbidResult();

            if (request.Status != VendorConnectionStatus.Pending)
                return new BadRequestObjectResult(new { Success = false, Message = "Request is no longer pending" });

            await _databaseLayer.UpdateConnectionRequestStatusAsync(requestId, VendorConnectionStatus.Rejected);
            return new OkObjectResult(new { Success = true, Message = "Request rejected" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> SendVendorChatMessage(int threadId, string content, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return new BadRequestObjectResult(new { Success = false, Message = "Message cannot be empty" });

        try
        {
            if (!await _databaseLayer.IsUserInThreadAsync(threadId, userId))
                return new ForbidResult();

            var messageId = await _databaseLayer.CreateChatMessageAsync(threadId, userId, content);
            var appUser = await _userManager.FindByIdAsync(userId);
            var senderName = appUser?.FullName ?? appUser?.Email ?? appUser?.UserName ?? "You";

            return new OkObjectResult(new
            {
                Success = true,
                Message = "Message sent",
                Data = new
                {
                    Id = messageId,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    SenderName = senderName,
                    IsMine = true,
                    DeletedForEveryone = false,
                    EditedAt = (DateTime?)null
                }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> EditVendorChatMessage(int messageId, string content, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return new BadRequestObjectResult(new { Success = false, Message = "Message cannot be empty" });

        try
        {
            var message = await _databaseLayer.GetChatMessageByIdAsync(messageId);
            if (message == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Message not found" });

            if (!await _databaseLayer.IsUserInThreadAsync(message.ThreadId, userId))
                return new ForbidResult();

            if (message.SenderUserId != userId)
                return new ForbidResult();

            if (message.DeletedForEveryone)
                return new BadRequestObjectResult(new { Success = false, Message = "Message was deleted" });

            await _databaseLayer.UpdateChatMessageContentAsync(messageId, content);

            return new OkObjectResult(new
            {
                Success = true,
                Message = "Message updated",
                Data = new
                {
                    Id = messageId,
                    Content = content,
                    EditedAt = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> HideVendorChatMessage(int messageId, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        try
        {
            var message = await _databaseLayer.GetChatMessageByIdAsync(messageId);
            if (message == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Message not found" });

            if (!await _databaseLayer.IsUserInThreadAsync(message.ThreadId, userId))
                return new ForbidResult();

            await _databaseLayer.HideChatMessageForUserAsync(messageId, userId);
            return new OkObjectResult(new { Success = true, Message = "Message hidden for you" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> DeleteVendorChatMessageForEveryone(int messageId, ClaimsPrincipal user)
    {
        var gate = await EnsureApprovedVendorAsync(user);
        if (gate != null) return gate;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return new ForbidResult();

        try
        {
            var message = await _databaseLayer.GetChatMessageByIdAsync(messageId);
            if (message == null)
                return new NotFoundObjectResult(new { Success = false, Message = "Message not found" });

            if (!await _databaseLayer.IsUserInThreadAsync(message.ThreadId, userId))
                return new ForbidResult();

            if (message.SenderUserId != userId)
                return new ForbidResult();

            await _databaseLayer.DeleteChatMessageForEveryoneAsync(messageId);
            return new OkObjectResult(new { Success = true, Message = "Message deleted for everyone" });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { Success = false, Message = ex.Message }) { StatusCode = 500 };
        }
    }
}
