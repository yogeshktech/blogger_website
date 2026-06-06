using Blogger_website.Models.Entities;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<BloggerUserDto>> GetAllBloggersAsync();
    Task<List<BloggerUserDto>> GetPendingBloggersAsync();
    Task<bool> SetUserActiveStatusAsync(string userId, bool isActive);
}

public class BloggerUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public bool IsActive { get; set; }
    public int BlogCount { get; set; }
}

public partial class DatabaseLayer
{
    public async Task<List<BloggerUserDto>> GetAllBloggersAsync()
    {
        var users = new List<BloggerUserDto>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT u."Id", u."Email", u."FullName", u."IsActive",
                   (SELECT COUNT(*) FROM "Blogs" b WHERE b."CreatedByUserId" = u."Id") AS "BlogCount"
            FROM "AspNetUsers" u
            INNER JOIN "AspNetUserRoles" ur ON u."Id" = ur."UserId"
            INNER JOIN "AspNetRoles" r ON ur."RoleId" = r."Id"
            WHERE r."Name" = 'Blogger'
            ORDER BY u."Email"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new BloggerUserDto
            {
                Id = reader.GetString(0),
                Email = reader.GetString(1),
                FullName = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsActive = reader.GetBoolean(3),
                BlogCount = Convert.ToInt32(reader.GetValue(4))
            });
        }

        return users;
    }

    public async Task<List<BloggerUserDto>> GetPendingBloggersAsync()
    {
        var users = new List<BloggerUserDto>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            SELECT u."Id", u."Email", u."FullName", u."IsActive",
                   (SELECT COUNT(*) FROM "Blogs" b WHERE b."CreatedByUserId" = u."Id") AS "BlogCount"
            FROM "AspNetUsers" u
            INNER JOIN "AspNetUserRoles" ur ON u."Id" = ur."UserId"
            INNER JOIN "AspNetRoles" r ON ur."RoleId" = r."Id"
            WHERE r."Name" = 'Blogger' AND u."IsActive" = FALSE AND u."EmailConfirmed" = TRUE
            ORDER BY u."Email"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new BloggerUserDto
            {
                Id = reader.GetString(0),
                Email = reader.GetString(1),
                FullName = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsActive = reader.GetBoolean(3),
                BlogCount = Convert.ToInt32(reader.GetValue(4))
            });
        }

        return users;
    }

    public async Task<bool> SetUserActiveStatusAsync(string userId, bool isActive)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """UPDATE "AspNetUsers" SET "IsActive" = @isActive WHERE "Id" = @userId""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("isActive", isActive);
        cmd.Parameters.AddWithValue("userId", userId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
