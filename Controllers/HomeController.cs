using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Models.Helpers;
using Blogger_website.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Blogger_website.Controllers;

public class HomeController : Controller
{
    private readonly IDatabaseLayer _databaseLayer;

    public HomeController(IDatabaseLayer databaseLayer)
    {
        _databaseLayer = databaseLayer;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var allPublished = await _databaseLayer.GetPublishedBlogsAsync(categoryId, null);
        var blogs = categoryId.HasValue ? allPublished : allPublished.Take(6).ToList();
        await _databaseLayer.AttachBlogLikesAsync(blogs, BlogEngagementHelper.GetLikeKey(HttpContext));

        var flat = await _databaseLayer.GetCategoriesFlatAsync();

        var model = new HomeViewModel
        {
            CategoryTree = await _databaseLayer.GetCategoriesAsync(),
            Labels = await _databaseLayer.GetLabelsAsync(),
            SelectedCategoryId = categoryId,
            Blogs = blogs
        };

        if (categoryId.HasValue)
        {
            var lookup = flat.ToDictionary(c => c.Id);
            model.SelectedCategoryPath = CategoryHelper.GetPath(categoryId.Value, lookup);
        }

        ViewData["Title"] = "Home";
        return View(model);
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Privacy";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Models.ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
