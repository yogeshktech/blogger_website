using Blogger_website.Models.Entities;
using Blogger_website.Models.Helpers;
using Npgsql;

namespace Blogger_website.Models.DatabaseLayer;

public partial interface IDatabaseLayer
{
    Task<List<BlogCategory>> GetCategoriesAsync(bool activeOnly = true);
    Task<List<BlogCategory>> GetCategoriesFlatAsync(bool activeOnly = true);
    Task<List<BlogCategory>> GetCategoryChildrenAsync(int? parentId, bool activeOnly = true);
    Task<BlogCategory?> GetCategoryByIdAsync(int id);
    Task<int> CreateCategoryAsync(string name, int? parentId, int sortOrder = 0);
    Task<bool> UpdateCategoryAsync(int id, string name, int? parentId, bool isActive, int sortOrder = 0);
    Task<bool> DeleteCategoryAsync(int id);
    Task<List<int>> GetCategoryFilterIdsAsync(int categoryId);
}

public partial class DatabaseLayer
{
    public async Task<List<BlogCategory>> GetCategoriesAsync(bool activeOnly = true)
    {
        var flat = await GetCategoriesFlatAsync(activeOnly);
        return CategoryHelper.BuildTree(flat);
    }

    public async Task<List<BlogCategory>> GetCategoriesFlatAsync(bool activeOnly = true)
    {
        var categories = new List<BlogCategory>();
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        var sql = """
            SELECT c."Id", c."Name", c."Slug", c."ParentId", p."Name" AS "ParentName",
                   c."SortOrder", c."IsActive", c."CreatedAt"
            FROM "BlogCategories" c
            LEFT JOIN "BlogCategories" p ON c."ParentId" = p."Id"
            """;

        if (activeOnly)
            sql += " WHERE c.\"IsActive\" = TRUE";

        sql += " ORDER BY c.\"SortOrder\", c.\"Name\"";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            categories.Add(MapCategory(reader));

        var lookup = categories.ToDictionary(c => c.Id);
        foreach (var cat in categories)
        {
            cat.FullPath = CategoryHelper.GetPath(cat.Id, lookup);
            cat.Depth = CategoryHelper.GetDepth(cat.Id, lookup);
        }

        return categories;
    }

    public async Task<List<BlogCategory>> GetCategoryChildrenAsync(int? parentId, bool activeOnly = true)
    {
        var flat = await GetCategoriesFlatAsync(activeOnly);
        return flat
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<BlogCategory?> GetCategoryByIdAsync(int id)
    {
        var flat = await GetCategoriesFlatAsync(activeOnly: false);
        return flat.FirstOrDefault(c => c.Id == id);
    }

    public async Task<int> CreateCategoryAsync(string name, int? parentId, int sortOrder = 0)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO "BlogCategories" ("Name", "Slug", "ParentId", "SortOrder", "IsActive", "CreatedAt")
            VALUES (@name, @slug, @parentId, @sortOrder, TRUE, @createdAt)
            RETURNING "Id"
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name.Trim());
        cmd.Parameters.AddWithValue("slug", Slugify(name));
        cmd.Parameters.AddWithValue("parentId", (object?)parentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sortOrder", sortOrder);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> UpdateCategoryAsync(int id, string name, int? parentId, bool isActive, int sortOrder = 0)
    {
        if (parentId == id) return false;

        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """
            UPDATE "BlogCategories" SET
                "Name" = @name, "Slug" = @slug, "ParentId" = @parentId,
                "SortOrder" = @sortOrder, "IsActive" = @isActive
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name.Trim());
        cmd.Parameters.AddWithValue("slug", Slugify(name));
        cmd.Parameters.AddWithValue("parentId", (object?)parentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sortOrder", sortOrder);
        cmd.Parameters.AddWithValue("isActive", isActive);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        await using var conn = new NpgsqlConnection(DbConnection);
        await conn.OpenAsync();

        const string sql = """DELETE FROM "BlogCategories" WHERE "Id" = @id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<int>> GetCategoryFilterIdsAsync(int categoryId)
    {
        var flat = await GetCategoriesFlatAsync(activeOnly: false);
        return CategoryHelper.GetDescendantIds(categoryId, flat).ToList();
    }

    private static BlogCategory MapCategory(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Slug = reader.IsDBNull(reader.GetOrdinal("Slug")) ? null : reader.GetString(reader.GetOrdinal("Slug")),
        ParentId = reader.IsDBNull(reader.GetOrdinal("ParentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentId")),
        ParentName = reader.IsDBNull(reader.GetOrdinal("ParentName")) ? null : reader.GetString(reader.GetOrdinal("ParentName")),
        SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
    };

    private static string Slugify(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-');
}
