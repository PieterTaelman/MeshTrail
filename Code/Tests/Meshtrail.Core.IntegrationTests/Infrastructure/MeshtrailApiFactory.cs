using Meshtrail.Mesh.Radio;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real WebApi in memory against the test database, signed in as a fixed development user.
/// The radio is replaced by <see cref="FakeMeshRadio"/>, so tests can inject mesh packets and see what was sent.
/// </summary>
internal sealed class MeshtrailApiFactory : WebApplicationFactory<Program>
{
    public const string TestUser = "integration-test";

    public FakeMeshRadio Radio { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MeshtrailDatabase", TestDatabase.ConnectionString);
        builder.UseSetting("Authentication:Mode", "Development");
        builder.UseSetting("Authentication:DevelopmentUser", TestUser);

        // No duty-cycle pause between test packets, and no scheduled clean-up during the run.
        builder.UseSetting("Meshtastic:Outbound:MinInterval", "00:00:00");
        builder.UseSetting("Jobs:NodePositionRetention:Enabled", "false");

        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IMeshRadio>(Radio)));
    }
}
