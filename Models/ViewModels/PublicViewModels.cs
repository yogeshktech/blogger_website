using Blogger_website.Models.Entities;

namespace Blogger_website.Models.ViewModels;

public class BlogListViewModel
{
    public List<Blog> Blogs { get; set; } = [];
    public List<BlogCategory> CategoryTree { get; set; } = [];
    public List<BlogCategory> CategoriesFlat { get; set; } = [];
    public List<BlogLabel> Labels { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public int? SelectedLabelId { get; set; }
}

public class BlogDetailViewModel
{
    public Blog Blog { get; set; } = null!;
    public List<Comment> Comments { get; set; } = [];
    public List<Comment> CommentThreads { get; set; } = [];
    public int TotalCommentCount { get; set; }
    public bool CanModerateComments { get; set; }
}

public class HomeViewModel
{
    public List<Blog> Blogs { get; set; } = [];
    public List<BlogCategory> CategoryTree { get; set; } = [];
    public List<BlogLabel> Labels { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public string? SelectedCategoryPath { get; set; }
}
