using Blogger_website.Models.Entities;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<Comment>> GetCommentsByBlogIdAsync(int blogId, bool approvedOnly = true);
    Task<List<Comment>> GetAllCommentsAsync(string? blogOwnerUserId = null);
    Task<Comment?> GetCommentByIdAsync(int id);
    Task<int> CreateCommentAsync(Comment comment);
    Task<bool> UpdateCommentApprovalAsync(int id, bool isApproved);
    Task<bool> UpdateCommentContentAsync(int id, string content);
    Task<bool> DeleteCommentAsync(int id);
}

public partial class DatabaseLayer
{
    public async Task<List<Comment>> GetCommentsByBlogIdAsync(int blogId, bool approvedOnly = true)
    {
        var comments = new List<Comment>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT c."Id", c."BlogId", c."UserId",
                   COALESCE(NULLIF(TRIM(c."AuthorName"), ''), u."FullName", u."Email", u."UserName", 'Guest') AS "AuthorName",
                   c."AuthorEmail",
                   c."Content", c."IsApproved", c."CreatedAt", c."ParentId"
            FROM "Comments" c
            LEFT JOIN "AspNetUsers" u ON c."UserId" = u."Id"
            WHERE c."BlogId" = @blogId
            """;

        if (approvedOnly)
            sql += " AND c.\"IsApproved\" = TRUE";

        sql += " ORDER BY c.\"CreatedAt\" ASC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("blogId", blogId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            comments.Add(MapComment(reader));

        return comments;
    }

    public async Task<List<Comment>> GetAllCommentsAsync(string? blogOwnerUserId = null)
    {
        var comments = new List<Comment>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT c."Id", c."BlogId", c."UserId", c."AuthorName", c."AuthorEmail",
                   c."Content", c."IsApproved", c."CreatedAt", c."ParentId",
                   b."Title" AS "BlogTitle"
            FROM "Comments" c
            INNER JOIN "Blogs" b ON c."BlogId" = b."Id"
            """;

        if (!string.IsNullOrEmpty(blogOwnerUserId))
            sql += " WHERE b.\"CreatedByUserId\" = @ownerId";

        sql += " ORDER BY c.\"CreatedAt\" DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(blogOwnerUserId))
            cmd.Parameters.AddWithValue("ownerId", blogOwnerUserId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            comments.Add(MapComment(reader, includeBlogTitle: true));

        return comments;
    }

    public async Task<Comment?> GetCommentByIdAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT c."Id", c."BlogId", c."UserId", c."AuthorName", c."AuthorEmail",
                   c."Content", c."IsApproved", c."CreatedAt", c."ParentId",
                   b."Title" AS "BlogTitle"
            FROM "Comments" c
            INNER JOIN "Blogs" b ON c."BlogId" = b."Id"
            WHERE c."Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapComment(reader, includeBlogTitle: true) : null;
    }

    public async Task<int> CreateCommentAsync(Comment comment)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "Comments"
            ("BlogId", "ParentId", "UserId", "AuthorName", "AuthorEmail", "Content", "IsApproved", "CreatedAt")
            VALUES
            (@blogId, @parentId, @userId, @authorName, @authorEmail, @content, @isApproved, @createdAt)
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("blogId", comment.BlogId);
        cmd.Parameters.AddWithValue("parentId", (object?)comment.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userId", (object?)comment.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authorName", comment.AuthorName);
        cmd.Parameters.AddWithValue("authorEmail", (object?)comment.AuthorEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("content", comment.Content);
        cmd.Parameters.AddWithValue("isApproved", comment.IsApproved);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpdateCommentApprovalAsync(int id, bool isApproved)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """UPDATE "Comments" SET "IsApproved" = @isApproved WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("isApproved", isApproved);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateCommentContentAsync(int id, string content)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """UPDATE "Comments" SET "Content" = @content WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteCommentAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """DELETE FROM "Comments" WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private static Comment MapComment(NpgsqlDataReader reader, bool includeBlogTitle = false)
    {
        var comment = new Comment
        {
            Id = reader.GetInt32(0),
            BlogId = reader.GetInt32(1),
            UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
            AuthorName = reader.GetString(3),
            AuthorEmail = reader.IsDBNull(4) ? null : reader.GetString(4),
            Content = reader.GetString(5),
            IsApproved = reader.GetBoolean(6),
            CreatedAt = reader.GetDateTime(7),
            ParentId = reader.IsDBNull(8) ? null : reader.GetInt32(8)
        };

        if (includeBlogTitle && reader.FieldCount > 9 && !reader.IsDBNull(9))
            comment.BlogTitle = reader.GetString(9);

        return comment;
    }
}
