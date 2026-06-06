namespace Blogger_website.Models.Entities;

public class Comment
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public int? ParentId { get; set; }
    public string? UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorEmail { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? BlogTitle { get; set; }
    public List<Comment> Replies { get; set; } = [];
}
