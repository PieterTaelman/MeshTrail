using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Infrastructure.Mesh;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real WebApi in memory against the test database, signed in as a fixed development user (an X-Dev-User
/// header acts as someone else). The MQTT broker is replaced by <see cref="FakeGatewayTransport"/>, so tests can
/// inject uplinks from any gateway and see what was sent where.
/// </summary>
internal sealed class MeshtrailApiFactory : WebApplicationFactory<Program>
{
    public const string TestUser = "integration-test";
    public const string ServiceKey = "test-service-key";

    public FakeGatewayTransport Transport { get; } = new();

    /// <summary>Every mail the API "sent" (confirmation and reset links).</summary>
    public CapturingEmailSender Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MeshtrailDatabase", TestDatabase.ConnectionString);
        builder.UseSetting("Authentication:Mode", "Development");
        builder.UseSetting("Authentication:DevelopmentUser", TestUser);

        // The broker's service key (protects the internal login check); the real MQTT transport stays off.
        builder.UseSetting("Meshtastic:Mqtt:Password", ServiceKey);

        // No duty-cycle pause between test packets, and no scheduled clean-up during the run.
        builder.UseSetting("Meshtastic:Outbound:MinInterval", "00:00:00");
        builder.UseSetting("Jobs:NodePositionRetention:Enabled", "false");

        // Real accounts work next to the development user: a request with a Bearer token is checked like in production.
        builder.UseSetting("Authentication:Local:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Authentication:Local:ClientBaseUrl", "https://client.test");
        builder.UseSetting("RateLimiting:AccountPerMinute", "10000");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IGatewayTransport>(Transport);
            services.AddSingleton<IEmailSender>(Mail);
        });
    }
}
