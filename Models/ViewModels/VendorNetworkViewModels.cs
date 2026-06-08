namespace Blogger_website.Models.ViewModels;

using Blogger_website.Models.Entities;

public class VendorListViewModel
{
    public List<VendorProfileDto> Vendors { get; set; } = [];
}

public class VendorProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string ConnectionStatus { get; set; } = "None";
    public int? RequestId { get; set; }
    public int? ThreadId { get; set; }
}

public class VendorRequestsViewModel
{
    public List<VendorConnectionRequest> Incoming { get; set; } = [];
    public List<VendorConnectionRequest> Outgoing { get; set; } = [];
}

public class VendorChatsViewModel
{
    public List<VendorChatThread> Threads { get; set; } = [];
}

public class VendorChatViewModel
{
    public VendorChatThread Thread { get; set; } = null!;
    public List<VendorChatMessage> Messages { get; set; } = [];
    public string OtherUserName { get; set; } = string.Empty;
    public string CurrentUserName { get; set; } = string.Empty;
    public string CurrentUserId { get; set; } = string.Empty;
}
