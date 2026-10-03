using Meshtrail.Database.Tooling;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// One real SQL Server database for the whole test run, built from the .sqlproj scripts.
/// Default: a throwaway SQL Server container (needs Docker). Set MESHTRAIL_TEST_SQL to a server
/// connection string (e.g. LocalDB) to use an existing server instead.
/// </summary>
internal static class TestDatabase
{
    public const string ServerConnectionStringVariable = "MESHTRAIL_TEST_SQL";
    private const string DatabaseName = "MeshtrailIntegrationTests";

    private static MsSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    public static async Task StartAsync(CancellationToken cancellationToken)
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ServerConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync(cancellationToken);
            serverConnectionString = _container.GetConnectionString();
        }

        var databaseDirectory = DatabaseSchema.FindDatabaseDirectory(AppContext.BaseDirectory);

        // Always rebuild: every run starts from an empty database with the current schema.
        await DatabaseSchema.EnsureAsync(serverConnectionString, DatabaseName, databaseDirectory, forceRebuild: true, cancellationToken);

        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = DatabaseName }.ConnectionString;
    }

    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
