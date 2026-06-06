namespace Blogger_website.Models.Entities;

public class BlogCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string LevelName => Depth switch
    {
        0 => "Category",
        1 => "Sub Category",
        _ => "Child Category"
    };
    public List<BlogCategory> Children { get; set; } = [];
}
