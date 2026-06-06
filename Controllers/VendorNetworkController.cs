using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.BusinessLayer;
using Blogger_website.Models.Constants;
using Blogger_website.Models.DatabaseLayer;
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
        var messages = await _databaseLayer.GetChatMessagesAsync(id);
        foreach (var msg in messages)
            msg.IsMine = msg.SenderUserId == userId;

        ViewData["Title"] = "Chat";
        return View(new VendorChatViewModel
        {
            Thread = thread,
            Messages = messages,
            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Vendor"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int threadId, string content)
    {
        var pending = await EnsureApprovedAsync();
        if (pending != null) return pending;

        var result = await _businessLayer.SendVendorChatMessage(threadId, content, User);
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

        var messages = await _databaseLayer.GetChatMessagesAsync(threadId, afterId);
        foreach (var msg in messages)
            msg.IsMine = msg.SenderUserId == userId;

        return Json(new { success = true, data = messages });
    }
}
