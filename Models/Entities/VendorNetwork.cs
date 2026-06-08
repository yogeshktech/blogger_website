namespace Blogger_website.Models.Entities;

public class VendorConnectionRequest
{
    public int Id { get; set; }
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? IntroMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? FromUserName { get; set; }
    public string? FromUserEmail { get; set; }
    public string? ToUserName { get; set; }
    public string? ToUserEmail { get; set; }
}

public class VendorChatThread
{
    public int Id { get; set; }
    public int ConnectionRequestId { get; set; }
    public string User1Id { get; set; } = string.Empty;
    public string User2Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class VendorChatMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? SenderName { get; set; }
    public bool IsMine { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool DeletedForEveryone { get; set; }
    public int? ReplyToMessageId { get; set; }
    public string? ReplyToSenderName { get; set; }
    public string? ReplyToContent { get; set; }
    public bool ReplyToDeleted { get; set; }
}

public static class VendorConnectionStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
}
