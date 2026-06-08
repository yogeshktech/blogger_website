using Microsoft.Extensions.Configuration;
using Npgsql;

var root = FindProjectRoot(AppContext.BaseDirectory)
    ?? FindProjectRoot(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException("Could not find project root (appsettings.json).");

var appsettings = Path.Combine(root, "appsettings.json");

var config = new ConfigurationBuilder()
    .AddJsonFile(appsettings, optional: false)
    .Build();

var connectionString = config.GetConnectionString("AppDbContextConnection")
    ?? throw new InvalidOperationException("AppDbContextConnection not found in appsettings.json");

var sqlDir = Path.Combine(root, "Others", "SQL");
var upgradePath = Path.Combine(sqlDir, "Upgrade_Existing_Database.sql");
var fullSchemaPath = Path.Combine(sqlDir, "Sql_file");

Console.WriteLine("BlogHub Database Upgrade");
Console.WriteLine("Connection: {0}", MaskConnection(connectionString));
Console.WriteLine();

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
Console.WriteLine("Connected OK.");
Console.WriteLine();

var tables = await ListTablesAsync(conn);
Console.WriteLine("Existing tables ({0}): {1}", tables.Count, string.Join(", ", tables));
Console.WriteLine();

var hasCore = tables.Contains("AspNetUsers") && tables.Contains("Blogs");
string sqlPath;
if (!hasCore)
{
    Console.WriteLine("Core tables missing — running FULL schema (Sql_file)...");
    sqlPath = fullSchemaPath;
}
else
{
    Console.WriteLine("Core tables found — running UPGRADE script...");
    sqlPath = upgradePath;
}

if (!File.Exists(sqlPath))
    throw new FileNotFoundException("SQL file not found", sqlPath);

var sql = await File.ReadAllTextAsync(sqlPath);
Console.WriteLine("Executing: {0}", Path.GetFileName(sqlPath));
Console.WriteLine(new string('-', 50));

await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 300 };
await cmd.ExecuteNonQueryAsync();

Console.WriteLine(new string('-', 50));
Console.WriteLine("SQL executed successfully!");
Console.WriteLine();

var after = await ListTablesAsync(conn);
var added = after.Except(tables).OrderBy(t => t).ToList();
if (added.Count > 0)
    Console.WriteLine("New tables added: {0}", string.Join(", ", added));

Console.WriteLine();
Console.WriteLine("All tables now ({0}):", after.Count);
foreach (var t in after)
    Console.WriteLine("  - {0}", t);

static string? FindProjectRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "appsettings.json"))
            && File.Exists(Path.Combine(dir.FullName, "Blogger_website.csproj")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

static string MaskConnection(string cs)
{
    var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return string.Join("; ", parts.Select(p =>
        p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ? "Password=****" : p));
}

static async Task<HashSet<string>> ListTablesAsync(NpgsqlConnection conn)
{
    const string sql = """
        SELECT tablename FROM pg_tables
        WHERE schemaname = 'public'
        ORDER BY tablename
        """;
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (await reader.ReadAsync())
        set.Add(reader.GetString(0));
    return set;
}
