using Blogger_website.Models.Entities;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<(bool Liked, int Count)> ToggleBlogLikeAsync(int blogId, string likedByKey);
    Task AttachBlogLikesAsync(IEnumerable<Blog> blogs, string? likedByKey);
}

public partial class DatabaseLayer
{
    public async Task<(bool Liked, int Count)> ToggleBlogLikeAsync(int blogId, string likedByKey)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string checkSql = """
            SELECT "Id" FROM "BlogLikes"
            WHERE "BlogId" = @blogId AND "LikedByKey" = @likedByKey
            """;

        await using var checkCmd = new NpgsqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("blogId", blogId);
        checkCmd.Parameters.AddWithValue("likedByKey", likedByKey);

        var existingId = await checkCmd.ExecuteScalarAsync();

        if (existingId != null)
        {
            const string deleteSql = """DELETE FROM "BlogLikes" WHERE "Id" = @id""";
            await using var deleteCmd = new NpgsqlCommand(deleteSql, conn);
            deleteCmd.Parameters.AddWithValue("id", Convert.ToInt32(existingId));
            await deleteCmd.ExecuteNonQueryAsync();
        }
        else
        {
            const string insertSql = """
                INSERT INTO "BlogLikes" ("BlogId", "LikedByKey", "CreatedAt")
                VALUES (@blogId, @likedByKey, @createdAt)
                """;
            await using var insertCmd = new NpgsqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("blogId", blogId);
            insertCmd.Parameters.AddWithValue("likedByKey", likedByKey);
            insertCmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await insertCmd.ExecuteNonQueryAsync();
        }

        var count = await GetBlogLikeCountInternalAsync(conn, blogId);
        return (existingId == null, count);
    }

    public async Task AttachBlogLikesAsync(IEnumerable<Blog> blogs, string? likedByKey)
    {
        var list = blogs as IList<Blog> ?? blogs.ToList();
        if (list.Count == 0) return;

        var ids = list.Select(b => b.Id).ToArray();

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var countSql = """
            SELECT "BlogId", COUNT(*)::int
            FROM "BlogLikes"
            WHERE "BlogId" = ANY(@ids)
            GROUP BY "BlogId"
            """;

        await using var countCmd = new NpgsqlCommand(countSql, conn);
        countCmd.Parameters.AddWithValue("ids", ids);

        var counts = new Dictionary<int, int>();
        await using (var reader = await countCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                counts[reader.GetInt32(0)] = reader.GetInt32(1);
        }

        HashSet<int>? likedIds = null;
        if (!string.IsNullOrEmpty(likedByKey))
        {
            likedIds = new HashSet<int>();
            const string likedSql = """
                SELECT "BlogId" FROM "BlogLikes"
                WHERE "BlogId" = ANY(@ids) AND "LikedByKey" = @likedByKey
                """;
            await using var likedCmd = new NpgsqlCommand(likedSql, conn);
            likedCmd.Parameters.AddWithValue("ids", ids);
            likedCmd.Parameters.AddWithValue("likedByKey", likedByKey);
            await using var likedReader = await likedCmd.ExecuteReaderAsync();
            while (await likedReader.ReadAsync())
                likedIds.Add(likedReader.GetInt32(0));
        }

        foreach (var blog in list)
        {
            blog.LikeCount = counts.GetValueOrDefault(blog.Id);
            blog.IsLikedByViewer = likedIds?.Contains(blog.Id) ?? false;
        }
    }

    private static async Task<int> GetBlogLikeCountInternalAsync(NpgsqlConnection conn, int blogId)
    {
        const string sql = """SELECT COUNT(*)::int FROM "BlogLikes" WHERE "BlogId" = @blogId""";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("blogId", blogId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
