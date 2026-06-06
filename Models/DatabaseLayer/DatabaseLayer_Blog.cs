using Blogger_website.Models.Entities;
using Blogger_website.Models.Helpers;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<Blog>> GetPublishedBlogsAsync(int? categoryId = null, int? labelId = null);
    Task<Blog?> GetBlogBySlugAsync(string slug, bool publishedOnly = true);
    Task<Blog?> GetBlogByIdAsync(int id);
    Task<List<Blog>> GetBlogsByUserIdAsync(string userId);
    Task<List<Blog>> GetAllBlogsAsync();
    Task<int> CreateBlogAsync(Blog blog);
    Task<bool> UpdateBlogAsync(Blog blog);
    Task<bool> DeleteBlogAsync(int id);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
}

public partial class DatabaseLayer
{
    public async Task<List<Blog>> GetPublishedBlogsAsync(int? categoryId = null, int? labelId = null)
    {
        var blogs = new List<Blog>();
        int[]? categoryIds = null;
        if (categoryId.HasValue)
            categoryIds = (await GetCategoryFilterIdsAsync(categoryId.Value)).ToArray();

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT DISTINCT b."Id", b."Title", b."Slug", b."ShortDescription", b."Content",
                   b."FeaturedImage", b."CategoryId", c."Name" AS "CategoryName",
                   b."CreatedByUserId", u."FullName" AS "AuthorName",
                   b."IsPublished", b."CreatedAt", b."UpdatedAt"
            FROM "Blogs" b
            LEFT JOIN "BlogCategories" c ON b."CategoryId" = c."Id"
            LEFT JOIN "AspNetUsers" u ON b."CreatedByUserId" = u."Id"
            """;

        if (labelId.HasValue)
            sql += "\nINNER JOIN \"BlogLabelMappings\" lm ON b.\"Id\" = lm.\"BlogId\" AND lm.\"LabelId\" = @labelId";

        sql += "\nWHERE b.\"IsPublished\" = TRUE";

        if (categoryIds is { Length: > 0 })
            sql += " AND b.\"CategoryId\" = ANY(@categoryIds)";

        sql += " ORDER BY b.\"CreatedAt\" DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (labelId.HasValue)
            cmd.Parameters.AddWithValue("labelId", labelId.Value);
        if (categoryIds is { Length: > 0 })
            cmd.Parameters.AddWithValue("categoryIds", categoryIds);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            blogs.Add(MapBlog(reader));

        await EnrichBlogsAsync(blogs);
        return blogs;
    }

    public async Task<Blog?> GetBlogBySlugAsync(string slug, bool publishedOnly = true)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT b."Id", b."Title", b."Slug", b."ShortDescription", b."Content",
                   b."FeaturedImage", b."CategoryId", c."Name" AS "CategoryName",
                   b."CreatedByUserId", u."FullName" AS "AuthorName",
                   b."IsPublished", b."CreatedAt", b."UpdatedAt"
            FROM "Blogs" b
            LEFT JOIN "BlogCategories" c ON b."CategoryId" = c."Id"
            LEFT JOIN "AspNetUsers" u ON b."CreatedByUserId" = u."Id"
            WHERE b."Slug" = @slug
            """;

        if (publishedOnly)
            sql += " AND b.\"IsPublished\" = TRUE";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("slug", slug);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var blog = MapBlog(reader);
        await EnrichBlogAsync(blog);
        return blog;
    }

    public async Task<Blog?> GetBlogByIdAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT b."Id", b."Title", b."Slug", b."ShortDescription", b."Content",
                   b."FeaturedImage", b."CategoryId", c."Name" AS "CategoryName",
                   b."CreatedByUserId", u."FullName" AS "AuthorName",
                   b."IsPublished", b."CreatedAt", b."UpdatedAt"
            FROM "Blogs" b
            LEFT JOIN "BlogCategories" c ON b."CategoryId" = c."Id"
            LEFT JOIN "AspNetUsers" u ON b."CreatedByUserId" = u."Id"
            WHERE b."Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var blog = MapBlog(reader);
        await EnrichBlogAsync(blog);
        return blog;
    }

    public async Task<List<Blog>> GetBlogsByUserIdAsync(string userId)
    {
        var blogs = new List<Blog>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT b."Id", b."Title", b."Slug", b."ShortDescription", b."Content",
                   b."FeaturedImage", b."CategoryId", c."Name" AS "CategoryName",
                   b."CreatedByUserId", u."FullName" AS "AuthorName",
                   b."IsPublished", b."CreatedAt", b."UpdatedAt"
            FROM "Blogs" b
            LEFT JOIN "BlogCategories" c ON b."CategoryId" = c."Id"
            LEFT JOIN "AspNetUsers" u ON b."CreatedByUserId" = u."Id"
            WHERE b."CreatedByUserId" = @userId
            ORDER BY b."CreatedAt" DESC
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            blogs.Add(MapBlog(reader));

        await EnrichBlogsAsync(blogs);
        return blogs;
    }

    public async Task<List<Blog>> GetAllBlogsAsync()
    {
        var blogs = new List<Blog>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT b."Id", b."Title", b."Slug", b."ShortDescription", b."Content",
                   b."FeaturedImage", b."CategoryId", c."Name" AS "CategoryName",
                   b."CreatedByUserId", u."FullName" AS "AuthorName",
                   b."IsPublished", b."CreatedAt", b."UpdatedAt"
            FROM "Blogs" b
            LEFT JOIN "BlogCategories" c ON b."CategoryId" = c."Id"
            LEFT JOIN "AspNetUsers" u ON b."CreatedByUserId" = u."Id"
            ORDER BY b."CreatedAt" DESC
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            blogs.Add(MapBlog(reader));

        await EnrichBlogsAsync(blogs);
        return blogs;
    }

    public async Task<int> CreateBlogAsync(Blog blog)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "Blogs"
            ("Title", "Slug", "ShortDescription", "Content", "FeaturedImage",
             "CategoryId", "CreatedByUserId", "IsPublished", "CreatedAt")
            VALUES
            (@title, @slug, @shortDesc, @content, @image, @categoryId, @userId, @published, @createdAt)
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("title", blog.Title);
        cmd.Parameters.AddWithValue("slug", blog.Slug);
        cmd.Parameters.AddWithValue("shortDesc", (object?)blog.ShortDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("content", blog.Content);
        cmd.Parameters.AddWithValue("image", (object?)blog.FeaturedImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("categoryId", (object?)blog.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userId", blog.CreatedByUserId);
        cmd.Parameters.AddWithValue("published", blog.IsPublished);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdateBlogAsync(Blog blog)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "Blogs" SET
                "Title" = @title,
                "Slug" = @slug,
                "ShortDescription" = @shortDesc,
                "Content" = @content,
                "FeaturedImage" = @image,
                "CategoryId" = @categoryId,
                "IsPublished" = @published,
                "UpdatedAt" = @updatedAt
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("title", blog.Title);
        cmd.Parameters.AddWithValue("slug", blog.Slug);
        cmd.Parameters.AddWithValue("shortDesc", (object?)blog.ShortDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("content", blog.Content);
        cmd.Parameters.AddWithValue("image", (object?)blog.FeaturedImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("categoryId", (object?)blog.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("published", blog.IsPublished);
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", blog.Id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteBlogAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """DELETE FROM "Blogs" WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """SELECT COUNT(*) FROM "Blogs" WHERE "Slug" = @slug""";
        if (excludeId.HasValue)
            sql += " AND \"Id\" != @excludeId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("slug", slug);
        if (excludeId.HasValue)
            cmd.Parameters.AddWithValue("excludeId", excludeId.Value);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private async Task EnrichBlogAsync(Blog blog) => await EnrichBlogsAsync([blog]);

    private async Task EnrichBlogsAsync(List<Blog> blogs)
    {
        if (blogs.Count == 0) return;

        var categories = await GetCategoriesFlatAsync(activeOnly: false);
        var lookup = categories.ToDictionary(c => c.Id);

        foreach (var blog in blogs)
        {
            if (blog.CategoryId.HasValue)
            {
                blog.CategoryPath = CategoryHelper.GetPath(blog.CategoryId.Value, lookup);
                if (string.IsNullOrEmpty(blog.CategoryName) && lookup.TryGetValue(blog.CategoryId.Value, out var cat))
                    blog.CategoryName = cat.Name;
            }
        }

        await AttachLabelsToBlogsAsync(blogs);
    }

    private static Blog MapBlog(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Slug = reader.GetString(reader.GetOrdinal("Slug")),
        ShortDescription = reader.IsDBNull(reader.GetOrdinal("ShortDescription")) ? null : reader.GetString(reader.GetOrdinal("ShortDescription")),
        Content = reader.GetString(reader.GetOrdinal("Content")),
        FeaturedImage = reader.IsDBNull(reader.GetOrdinal("FeaturedImage")) ? null : reader.GetString(reader.GetOrdinal("FeaturedImage")),
        CategoryId = reader.IsDBNull(reader.GetOrdinal("CategoryId")) ? null : reader.GetInt32(reader.GetOrdinal("CategoryId")),
        CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
        CreatedByUserId = reader.GetString(reader.GetOrdinal("CreatedByUserId")),
        AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName")) ? null : reader.GetString(reader.GetOrdinal("AuthorName")),
        IsPublished = reader.GetBoolean(reader.GetOrdinal("IsPublished")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
    };
}
