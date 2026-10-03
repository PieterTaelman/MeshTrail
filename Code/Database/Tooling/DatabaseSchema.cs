using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Meshtrail.Database.Tooling;

/// <summary>What <see cref="DatabaseSchema.EnsureAsync"/> did, so callers can log it.</summary>
internal enum SchemaOutcome
{
    Unchanged,
    Rebuilt,
}

/// <summary>
/// Builds a database from the .sqlproj table scripts, for LOCAL DEVELOPMENT and TESTS only.
/// Shared (as linked source) by the AppHost and the integration tests so both use exactly the same schema.
/// The schema hash is stored as a database extended property: same hash = keep data, different hash = drop and rebuild.
/// </summary>
internal static partial class DatabaseSchema
{
    private const string HashPropertyName = "Meshtrail.SchemaHash";
    private const string ScriptListFileName = "Scripts_Core.txt";
    private const string ProjectFolderName = "Meshtrail.Database";

    /// <summary>Walks up from <paramref name="startDirectory"/> until it finds Code/Database (the folder with Scripts_Core.txt).</summary>
    public static string FindDatabaseDirectory(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Code", "Database");
            if (File.Exists(Path.Combine(candidate, ScriptListFileName)))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory.FullName, ScriptListFileName)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Could not find Code/Database/{ScriptListFileName} above '{startDirectory}'.");
    }

    /// <summary>Concatenates the table scripts in Scripts_Core.txt order into one script.</summary>
    public static string BuildSchemaScript(string databaseDirectory)
    {
        var projectDirectory = Path.Combine(databaseDirectory, ProjectFolderName);
        var builder = new StringBuilder();

        foreach (var line in File.ReadAllLines(Path.Combine(databaseDirectory, ScriptListFileName)))
        {
            var relativePath = line.Trim();
            if (relativePath.Length == 0 || relativePath.StartsWith('#'))
            {
                continue;
            }

            var scriptPath = Path.Combine(projectDirectory, relativePath);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"{ScriptListFileName} lists '{relativePath}', but that file does not exist.", scriptPath);
            }

            builder.Append("-- ").AppendLine(relativePath);
            builder.AppendLine(File.ReadAllText(scriptPath));
            builder.AppendLine("GO");
        }

        // Normalise line endings so a CRLF checkout gives the same hash as an LF checkout.
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static string ComputeHash(string script) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

    /// <summary>Seed scripts (Meshtrail.Database/Seed/*.sql) in file-name order. They must be idempotent.</summary>
    public static IReadOnlyList<string> GetSeedScripts(string databaseDirectory)
    {
        var seedDirectory = Path.Combine(databaseDirectory, ProjectFolderName, "Seed");
        return Directory.Exists(seedDirectory)
            ? [.. Directory.GetFiles(seedDirectory, "*.sql").Order(StringComparer.Ordinal).Select(File.ReadAllText)]
            : [];
    }

    /// <summary>
    /// Makes sure <paramref name="databaseName"/> exists with the current schema. Rebuilds it (all data lost)
    /// when the schema hash changed or when <paramref name="forceRebuild"/> is true.
    /// </summary>
    public static async Task<SchemaOutcome> EnsureAsync(
        string serverConnectionString,
        string databaseName,
        string databaseDirectory,
        bool forceRebuild,
        CancellationToken cancellationToken)
    {
        if (!SafeDatabaseName().IsMatch(databaseName))
        {
            throw new ArgumentException($"'{databaseName}' is not a safe database name.", nameof(databaseName));
        }

        var script = BuildSchemaScript(databaseDirectory);
        var hash = ComputeHash(script);

        await using var master = await OpenAsync(serverConnectionString, "master", cancellationToken);

        if (!forceRebuild && await ReadHashAsync(master, databaseName, cancellationToken) == hash)
        {
            return SchemaOutcome.Unchanged;
        }

        await ExecuteAsync(master, $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            CREATE DATABASE [{databaseName}];
            """, cancellationToken);

        // Old pooled connections point at the dropped database and would fail on first use.
        SqlConnection.ClearAllPools();

        await using var database = await OpenAsync(serverConnectionString, databaseName, cancellationToken);
        await ExecuteBatchesAsync(database, script, cancellationToken);
        await ExecuteAsync(database, $"EXEC sys.sp_addextendedproperty @name = N'{HashPropertyName}', @value = N'{hash}';", cancellationToken);

        return SchemaOutcome.Rebuilt;
    }

    public static async Task SeedAsync(string serverConnectionString, string databaseName, string databaseDirectory, CancellationToken cancellationToken)
    {
        await using var database = await OpenAsync(serverConnectionString, databaseName, cancellationToken);
        foreach (var seed in GetSeedScripts(databaseDirectory))
        {
            await ExecuteBatchesAsync(database, seed, cancellationToken);
        }
    }

    /// <summary>Runs a script that may contain GO separators (GO is a tool command, not T-SQL, so we split on it).</summary>
    public static async Task ExecuteBatchesAsync(SqlConnection connection, string script, CancellationToken cancellationToken)
    {
        foreach (var batch in GoSeparator().Split(script).Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await ExecuteAsync(connection, batch, cancellationToken);
        }
    }

    private static async Task<string?> ReadHashAsync(SqlConnection master, string databaseName, CancellationToken cancellationToken)
    {
        await using var exists = new SqlCommand($"SELECT DB_ID(N'{databaseName}')", master);
        if (await exists.ExecuteScalarAsync(cancellationToken) is DBNull or null)
        {
            return null;
        }

        await using var command = new SqlCommand(
            $"SELECT CAST(value AS nvarchar(128)) FROM [{databaseName}].sys.extended_properties WHERE class = 0 AND name = N'{HashPropertyName}'",
            master);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<SqlConnection> OpenAsync(string serverConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = databaseName };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$")]
    private static partial Regex SafeDatabaseName();
}
