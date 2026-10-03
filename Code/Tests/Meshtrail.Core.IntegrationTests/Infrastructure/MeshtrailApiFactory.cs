using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real WebApi in memory against the test database, signed in as a fixed development user.
/// </summary>
internal sealed class MeshtrailApiFactory : WebApplicationFactory<Program>
{
    public const string TestUser = "integration-test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MeshtrailDatabase", TestDatabase.ConnectionString);
        builder.UseSetting("Authentication:Mode", "Development");
        builder.UseSetting("Authentication:DevelopmentUser", TestUser);
    }
}
