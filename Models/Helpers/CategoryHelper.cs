using Blogger_website.Models.Entities;

namespace Blogger_website.Models.Helpers;

public static class CategoryHelper
{
    public static List<BlogCategory> BuildTree(IEnumerable<BlogCategory> flat)
    {
        var lookup = flat.ToDictionary(c => c.Id);
        foreach (var cat in flat)
        {
            cat.FullPath = GetPath(cat.Id, lookup);
            cat.Depth = GetDepth(cat.Id, lookup);
            cat.Children = [];
        }

        var roots = new List<BlogCategory>();
        foreach (var cat in flat.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
        {
            if (cat.ParentId.HasValue && lookup.TryGetValue(cat.ParentId.Value, out var parent))
                parent.Children.Add(cat);
            else
                roots.Add(cat);
        }

        return roots.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
    }

    public static string GetPath(int categoryId, IReadOnlyDictionary<int, BlogCategory> lookup)
    {
        var parts = new List<string>();
        int? currentId = categoryId;
        var guard = 0;

        while (currentId.HasValue && lookup.TryGetValue(currentId.Value, out var cat) && guard++ < 20)
        {
            parts.Insert(0, cat.Name);
            currentId = cat.ParentId;
        }

        return string.Join(" › ", parts);
    }

    public static int GetDepth(int categoryId, IReadOnlyDictionary<int, BlogCategory> lookup)
    {
        var depth = 0;
        int? currentId = categoryId;
        var guard = 0;

        while (currentId.HasValue && lookup.TryGetValue(currentId.Value, out var cat) && cat.ParentId.HasValue && guard++ < 20)
        {
            depth++;
            currentId = cat.ParentId;
        }

        return depth;
    }

    public static HashSet<int> GetDescendantIds(int categoryId, IEnumerable<BlogCategory> flat)
    {
        var ids = new HashSet<int> { categoryId };
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var cat in flat)
            {
                if (cat.ParentId.HasValue && ids.Contains(cat.ParentId.Value) && ids.Add(cat.Id))
                    changed = true;
            }
        }

        return ids;
    }
}
