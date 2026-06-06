using Blogger_website.Models.Entities;

namespace Blogger_website.Models.Helpers;

public static class CommentHelper
{
    public static List<Comment> BuildThread(IEnumerable<Comment> flat)
    {
        var list = flat.ToList();
        var lookup = list.ToDictionary(c => c.Id);

        foreach (var comment in list)
            comment.Replies = [];

        var roots = new List<Comment>();
        foreach (var comment in list.OrderBy(c => c.CreatedAt))
        {
            if (comment.ParentId is null or 0)
            {
                roots.Add(comment);
                continue;
            }

            if (lookup.TryGetValue(comment.ParentId.Value, out var parent))
                parent.Replies.Add(comment);
            else
                roots.Add(comment);
        }

        return roots;
    }

    public static int CountAll(IEnumerable<Comment> flat) => flat.Count();
}
