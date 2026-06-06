using Npgsql;

namespace Blogger_website.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppDbContextConnection");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        // Existing DB ke liye sirf missing tables/columns add karta hai
        var sqlPath = FindSqlFile("Upgrade_Existing_Database.sql")
                   ?? FindSqlFile("Sql_file");

        if (sqlPath == null) return;

        var sql = await File.ReadAllTextAsync(sqlPath);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? FindSqlFile(string fileName)
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Others", "SQL", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Others", "SQL", fileName)
        };

        return paths.FirstOrDefault(File.Exists);
    }
}
