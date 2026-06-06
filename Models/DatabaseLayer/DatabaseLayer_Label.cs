using Blogger_website.Models.Entities;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<BlogLabel>> GetLabelsAsync(bool activeOnly = true);
    Task<int> CreateLabelAsync(string name);
    Task<bool> UpdateLabelAsync(int id, string name, bool isActive);
    Task<bool> DeleteLabelAsync(int id);
    Task<List<BlogLabel>> GetLabelsByBlogIdAsync(int blogId);
    Task SetBlogLabelsAsync(int blogId, IEnumerable<int> labelIds);
    Task AttachLabelsToBlogsAsync(List<Blog> blogs);
}

public partial class DatabaseLayer
{
    public async Task<List<BlogLabel>> GetLabelsAsync(bool activeOnly = true)
    {
        var labels = new List<BlogLabel>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """SELECT "Id", "Name", "Slug", "IsActive", "CreatedAt" FROM "BlogLabels" """;
        if (activeOnly)
            sql += " WHERE \"IsActive\" = TRUE";
        sql += " ORDER BY \"Name\"";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            labels.Add(MapLabel(reader));

        return labels;
    }

    public async Task<int> CreateLabelAsync(string name)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "BlogLabels" ("Name", "Slug", "IsActive", "CreatedAt")
            VALUES (@name, @slug, TRUE, @createdAt)
            ON CONFLICT ("Name") DO NOTHING
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name.Trim());
        cmd.Parameters.AddWithValue("slug", name.Trim().ToLowerInvariant().Replace(' ', '-'));
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        var result = await cmd.ExecuteScalarAsync();
        if (result != null) return Convert.ToInt32(result);

        const string fetchSql = """SELECT "Id" FROM "BlogLabels" WHERE "Name" = @name""";
        await using var fetchCmd = new NpgsqlCommand(fetchSql, conn);
        fetchCmd.Parameters.AddWithValue("name", name.Trim());
        return Convert.ToInt32(await fetchCmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpdateLabelAsync(int id, string name, bool isActive)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "BlogLabels" SET "Name" = @name, "Slug" = @slug, "IsActive" = @isActive WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name.Trim());
        cmd.Parameters.AddWithValue("slug", name.Trim().ToLowerInvariant().Replace(' ', '-'));
        cmd.Parameters.AddWithValue("isActive", isActive);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteLabelAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """DELETE FROM "BlogLabels" WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<BlogLabel>> GetLabelsByBlogIdAsync(int blogId)
    {
        var labels = new List<BlogLabel>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT l."Id", l."Name", l."Slug", l."IsActive", l."CreatedAt"
            FROM "BlogLabels" l
            INNER JOIN "BlogLabelMappings" m ON l."Id" = m."LabelId"
            WHERE m."BlogId" = @blogId AND l."IsActive" = TRUE
            ORDER BY l."Name"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("blogId", blogId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            labels.Add(MapLabel(reader));

        return labels;
    }

    public async Task SetBlogLabelsAsync(int blogId, IEnumerable<int> labelIds)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        await using var delCmd = new NpgsqlCommand("""DELETE FROM "BlogLabelMappings" WHERE "BlogId" = @blogId""", conn);
        delCmd.Parameters.AddWithValue("blogId", blogId);
        await delCmd.ExecuteNonQueryAsync();

        foreach (var labelId in labelIds.Distinct())
        {
            await using var insCmd = new NpgsqlCommand(
                """INSERT INTO "BlogLabelMappings" ("BlogId", "LabelId") VALUES (@blogId, @labelId) ON CONFLICT DO NOTHING""", conn);
            insCmd.Parameters.AddWithValue("blogId", blogId);
            insCmd.Parameters.AddWithValue("labelId", labelId);
            await insCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task AttachLabelsToBlogsAsync(List<Blog> blogs)
    {
        if (blogs.Count == 0) return;

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var ids = blogs.Select(b => b.Id).ToArray();
        const string sql = """
            SELECT m."BlogId", l."Id", l."Name", l."Slug", l."IsActive", l."CreatedAt"
            FROM "BlogLabelMappings" m
            INNER JOIN "BlogLabels" l ON m."LabelId" = l."Id"
            WHERE m."BlogId" = ANY(@ids) AND l."IsActive" = TRUE
            ORDER BY l."Name"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        var map = blogs.ToDictionary(b => b.Id, b => b);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var blogId = reader.GetInt32(0);
            if (map.TryGetValue(blogId, out var blog))
            {
                blog.Labels.Add(new BlogLabel
                {
                    Id = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Slug = reader.IsDBNull(3) ? null : reader.GetString(3),
                    IsActive = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
        }
    }

    private static BlogLabel MapLabel(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Slug = reader.IsDBNull(reader.GetOrdinal("Slug")) ? null : reader.GetString(reader.GetOrdinal("Slug")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
    };
}
