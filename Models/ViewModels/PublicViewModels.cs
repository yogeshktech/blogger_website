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
    public CategoryBrowseViewModel CategoryBrowse { get; set; } = new();
}

public class BlogDetailViewModel
{
    public Blog Blog { get; set; } = null!;
    public List<Comment> Comments { get; set; } = [];
    public List<Comment> CommentThreads { get; set; } = [];
    public int TotalCommentCount { get; set; }
    public bool CanModerateComments { get; set; }
    public string? CurrentUserDisplayName { get; set; }
    public string? CurrentUserId { get; set; }
}

public class HomeViewModel
{
    public List<Blog> Blogs { get; set; } = [];
    public List<BlogCategory> CategoryTree { get; set; } = [];
    public List<BlogCategory> CategoriesFlat { get; set; } = [];
    public List<BlogLabel> Labels { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public string? SelectedCategoryPath { get; set; }
    public CategoryBrowseViewModel CategoryBrowse { get; set; } = new();
}

public class CategoryBrowseViewModel
{
    public List<BlogCategory> Cards { get; set; } = [];
    public List<CategoryBreadcrumbItem> Breadcrumb { get; set; } = [];
    public string LevelTitle { get; set; } = "Browse Categories";
    public bool ShowBlogs { get; set; } = true;
    public int? SelectedCategoryId { get; set; }
    public string ControllerName { get; set; } = "Home";
    public string ActionName { get; set; } = "Index";
    public int? LabelId { get; set; }
}

public record CategoryBreadcrumbItem(int? Id, string Name);
