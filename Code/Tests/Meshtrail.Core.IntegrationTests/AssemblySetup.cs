using Meshtrail.Core.IntegrationTests.Infrastructure;

// Tests share one database and one API host, so run them one at a time.
[assembly: DoNotParallelize]

namespace Meshtrail.Core.IntegrationTests;

[TestClass]
public static class AssemblySetup
{
    internal static MeshtrailApiFactory Factory { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        await TestDatabase.StartAsync(context.CancellationToken);
        Factory = new MeshtrailApiFactory();
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        await Factory.DisposeAsync();
        await TestDatabase.StopAsync();
    }
}
