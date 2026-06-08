using Blogger_website.Models.Entities;
using Blogger_website.Models.ViewModels;

namespace Blogger_website.Models.Helpers;

public static class CategoryBrowseHelper
{
    public static CategoryBrowseViewModel Build(
        List<BlogCategory> tree,
        List<BlogCategory> flat,
        int? categoryId,
        string controllerName,
        string actionName,
        int? labelId = null)
    {
        var lookup = flat.ToDictionary(c => c.Id);
        var vm = new CategoryBrowseViewModel
        {
            ControllerName = controllerName,
            ActionName = actionName,
            LabelId = labelId,
            SelectedCategoryId = categoryId,
            Breadcrumb = [new CategoryBreadcrumbItem(null, "Home")]
        };

        if (!categoryId.HasValue)
        {
            vm.Cards = tree;
            vm.LevelTitle = "Browse Categories";
            vm.ShowBlogs = true;
            return vm;
        }

        if (!lookup.TryGetValue(categoryId.Value, out var current))
        {
            vm.ShowBlogs = true;
            return vm;
        }

        vm.Breadcrumb = BuildBreadcrumb(flat, categoryId.Value);
        var node = FindNode(tree, categoryId.Value);

        if (node?.Children.Any(c => c.IsActive) == true)
        {
            vm.Cards = node.Children.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
            vm.LevelTitle = node.Depth switch
            {
                0 => $"Subcategories in {current.Name}",
                1 => $"Topics in {current.Name}",
                _ => current.Name
            };
            vm.ShowBlogs = false;
        }
        else
        {
            vm.Cards = [];
            vm.LevelTitle = current.Name;
            vm.ShowBlogs = true;
        }

        return vm;
    }

    private static List<CategoryBreadcrumbItem> BuildBreadcrumb(List<BlogCategory> flat, int categoryId)
    {
        var lookup = flat.ToDictionary(c => c.Id);
        var items = new List<CategoryBreadcrumbItem> { new(null, "Home") };
        var path = new List<int>();
        int? id = categoryId;
        var guard = 0;

        while (id.HasValue && lookup.TryGetValue(id.Value, out var cat) && guard++ < 20)
        {
            path.Insert(0, cat.Id);
            id = cat.ParentId;
        }

        foreach (var pid in path)
            items.Add(new CategoryBreadcrumbItem(pid, lookup[pid].Name));

        return items;
    }

    private static BlogCategory? FindNode(IEnumerable<BlogCategory> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            var found = FindNode(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }
}
