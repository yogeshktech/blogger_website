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
public class VendorNetworkController : Controller
{
    private readonly IBusinessLayer _businessLayer;
    private readonly IDatabaseLayer _databaseLayer;
    private readonly UserManager<ApplicationUser> _userManager;

    public VendorNetworkController(
        IBusinessLayer businessLayer,
        IDatabaseLayer databaseLayer,
        UserManager<ApplicationUser> userManager)
    {
        _businessLayer = businessLayer;
        _databaseLayer = databaseLayer;
        _userManager = userManager;
    }

    private async Task<IActionResult?> EnsureApprovedAsync()
    {
        if (User.IsInRole(AppRoles.SuperAdmin)) return null;
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
            return RedirectToAction("Index", "AdminPanel");
        return null;
    }

    public async Task<IActionResult> Vendors()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var userId = _userManager.GetUserId(User)!;
        ViewData["Title"] = "Vendor Network";
        return View(new VendorListViewModel
        {
            Vendors = await _databaseLayer.GetVendorProfilesAsync(userId)
        });
    }

    public async Task<IActionResult> Requests()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var userId = _userManager.GetUserId(User)!;
        ViewData["Title"] = "Connection Requests";
        return View(new VendorRequestsViewModel
        {
            Incoming = await _databaseLayer.GetIncomingConnectionRequestsAsync(userId),
            Outgoing = await _databaseLayer.GetOutgoingConnectionRequestsAsync(userId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(string toUserId, string? introMessage)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.SendVendorConnectionRequest(toUserId, introMessage, User);
        TempData[result is OkObjectResult ? "Success" : "Error"] = result is OkObjectResult
            ? "Connection request sent."
            : "Could not send request.";
        return RedirectToAction(nameof(Vendors));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRequest(int id)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.AcceptVendorConnectionRequest(id, User);
        if (result is OkObjectResult)
        {
            TempData["Success"] = "Request accepted. Chat is now open.";
            var request = await _databaseLayer.GetConnectionRequestByIdAsync(id);
            if (request != null)
            {
                var thread = await _databaseLayer.GetChatThreadByUsersAsync(request.FromUserId, request.ToUserId);
                if (thread != null)
                    return RedirectToAction(nameof(Chat), new { id = thread.Id });
            }
        }
        else
        {
            TempData["Error"] = "Could not accept request.";
        }

        return RedirectToAction(nameof(Requests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        await _businessLayer.RejectVendorConnectionRequest(id, User);
        TempData["Success"] = "Request rejected.";
        return RedirectToAction(nameof(Requests));
    }

    public async Task<IActionResult> Chats()
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var userId = _userManager.GetUserId(User)!;
        ViewData["Title"] = "Vendor Chats";
        return View(new VendorChatsViewModel
        {
            Threads = await _databaseLayer.GetChatThreadsForUserAsync(userId)
        });
    }

    public async Task<IActionResult> Chat(int id)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var userId = _userManager.GetUserId(User)!;
        if (!await _databaseLayer.IsUserInThreadAsync(id, userId))
            return NotFound();

        var thread = await _databaseLayer.GetChatThreadByIdAsync(id);
        if (thread == null) return NotFound();

        var otherUserId = thread.User1Id == userId ? thread.User2Id : thread.User1Id;
        var otherUser = await _userManager.FindByIdAsync(otherUserId);
        var currentUser = await _userManager.GetUserAsync(User);
        var messages = await _databaseLayer.GetChatMessagesAsync(id, null, userId);
        foreach (var msg in messages)
            msg.IsMine = string.Equals(msg.SenderUserId, userId, StringComparison.Ordinal);

        ViewData["Title"] = "Chat";
        return View(new VendorChatViewModel
        {
            Thread = thread,
            Messages = messages,
            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Vendor",
            CurrentUserName = currentUser?.FullName ?? currentUser?.Email ?? currentUser?.UserName ?? "You",
            CurrentUserId = userId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int threadId, string content)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.SendVendorChatMessage(threadId, content, User);
        if (result is OkObjectResult ok && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(ok.Value);
        }

        if (result is not OkObjectResult)
            TempData["Error"] = "Message could not be sent.";

        return RedirectToAction(nameof(Chat), new { id = threadId });
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(int threadId, int? afterId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId) || !await _databaseLayer.IsUserInThreadAsync(threadId, userId))
            return Forbid();

        var messages = await _databaseLayer.GetChatMessagesAsync(threadId, afterId, userId);
        foreach (var msg in messages)
            msg.IsMine = string.Equals(msg.SenderUserId, userId, StringComparison.Ordinal);

        return Json(new
        {
            success = true,
            data = messages.Select(m => new
            {
                id = m.Id,
                content = m.DeletedForEveryone ? string.Empty : m.Content,
                createdAt = m.CreatedAt,
                editedAt = m.EditedAt,
                senderName = m.SenderName,
                senderUserId = m.SenderUserId,
                isMine = m.IsMine,
                deletedForEveryone = m.DeletedForEveryone
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessage(int messageId, string content)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.EditVendorChatMessage(messageId, content, User);
        if (result is OkObjectResult ok)
            return Json(ok.Value);
        if (result is BadRequestObjectResult bad)
            return Json(bad.Value);
        return Json(new { success = false, message = "You can only edit your own messages" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideMessage(int messageId)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.HideVendorChatMessage(messageId, User);
        return result is OkObjectResult ok ? Json(ok.Value) : BadRequest(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessageForEveryone(int messageId)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.DeleteVendorChatMessageForEveryone(messageId, User);
        return result is OkObjectResult ok ? Json(ok.Value) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var incoming = await _databaseLayer.GetIncomingConnectionRequestsAsync(userId);
        var pendingIncoming = incoming.Count(r => r.Status == VendorConnectionStatus.Pending);

        var outgoing = await _databaseLayer.GetOutgoingConnectionRequestsAsync(userId);
        var acceptedOutgoing = outgoing
            .Where(r => r.Status == VendorConnectionStatus.Accepted)
            .Select(r => new { id = r.Id, toUserName = r.ToUserName, respondedAt = r.RespondedAt })
            .ToList();

        return Json(new { success = true, pendingIncoming, acceptedOutgoing });
    }
}
