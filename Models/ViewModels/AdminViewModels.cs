using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Models.Entities;

namespace Blogger_website.Models.ViewModels;

public class AdminDashboardViewModel
{
    public bool IsSuperAdmin { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int TotalBlogs { get; set; }
    public int PublishedBlogs { get; set; }
    public int DraftBlogs { get; set; }
    public int PendingAdmins { get; set; }
    public int TotalComments { get; set; }
    public List<Blog> RecentBlogs { get; set; } = [];
}

public class BlogFormViewModel
{
    public int? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? FeaturedImage { get; set; }
    public int? CategoryId { get; set; }
    public bool IsPublished { get; set; }
    public List<int> SelectedLabelIds { get; set; } = [];
    public List<BlogCategory> CategoriesFlat { get; set; } = [];
    public List<BlogCategory> RootCategories { get; set; } = [];
    public List<BlogLabel> Labels { get; set; } = [];
}

public class AdminBlogListViewModel
{
    public List<Blog> Blogs { get; set; } = [];
    public bool IsSuperAdmin { get; set; }
}

public class PendingAdminsViewModel
{
    public List<BloggerUserDto> Pending { get; set; } = [];
}

public class BloggersViewModel
{
    public List<BloggerUserDto> Bloggers { get; set; } = [];
}

public class CategoriesAdminViewModel
{
    public bool IsSuperAdmin { get; set; }
    public List<BlogCategory> CategoryTree { get; set; } = [];
    public List<BlogCategory> CategoriesFlat { get; set; } = [];
}

public class LabelsAdminViewModel
{
    public List<BlogLabel> Labels { get; set; } = [];
}

public class CommentsAdminViewModel
{
    public bool IsSuperAdmin { get; set; }
    public List<Comment> Comments { get; set; } = [];
}
