using Meshtrail.Database.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Meshtrail.AppHost;

/// <summary>
/// Keeps the local database in sync with the .sqlproj on every AppHost start:
/// schema unchanged = keep your data, schema changed = drop and rebuild. Then the seed scripts run.
/// </summary>
internal static class DatabaseResourceExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> WithSchemaFromSqlProject(
        this IResourceBuilder<SqlServerDatabaseResource> database,
        string appHostDirectory)
    {
        var databaseDirectory = DatabaseSchema.FindDatabaseDirectory(appHostDirectory);

        // Runs once the database accepts connections. The API waits for it (WaitFor) until this finishes.
        database.OnResourceReady((resource, readyEvent, cancellationToken) =>
            InitializeAsync(resource, readyEvent.Services, databaseDirectory, forceRebuild: false, cancellationToken));

        database.WithCommand(
            name: "rebuild-database",
            displayName: "Rebuild database",
            executeCommand: async context =>
            {
                await InitializeAsync(database.Resource, context.Services, databaseDirectory, forceRebuild: true, context.CancellationToken);
                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = "Drops the database, recreates it from the .sqlproj scripts and runs the seed scripts.",
                ConfirmationMessage = "This deletes ALL local data in MeshtrailDatabase. Continue?",
                IconName = "ArrowSync",
            });

        return database;
    }

    private static async Task InitializeAsync(
        SqlServerDatabaseResource resource,
        IServiceProvider services,
        string databaseDirectory,
        bool forceRebuild,
        CancellationToken cancellationToken)
    {
        // Logs show up on the database resource in the Aspire dashboard.
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
        var serverConnectionString = await resource.Parent.ConnectionStringExpression.GetValueAsync(cancellationToken)
            ?? throw new InvalidOperationException("SQL Server connection string is not available yet.");

        var outcome = await DatabaseSchema.EnsureAsync(serverConnectionString, resource.DatabaseName, databaseDirectory, forceRebuild, cancellationToken);
        logger.LogInformation(
            outcome == SchemaOutcome.Rebuilt
                ? "Schema changed (or rebuild requested): database {Database} was rebuilt from the .sqlproj scripts."
                : "Schema unchanged: kept existing data in {Database}.",
            resource.DatabaseName);

        await DatabaseSchema.SeedAsync(serverConnectionString, resource.DatabaseName, databaseDirectory, cancellationToken);
        logger.LogInformation("Seed scripts applied to {Database}.", resource.DatabaseName);
    }
}
