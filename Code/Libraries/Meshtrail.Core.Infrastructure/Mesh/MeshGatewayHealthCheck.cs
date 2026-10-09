using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Reports the gateway transports (MQTT broker, TCP node, simulator). Never "Unhealthy": the API works without them,
/// so a lost link is only "Degraded" and /health/ready keeps answering 200.
/// </summary>
public sealed class MeshGatewayHealthCheck(IEnumerable<IGatewayTransport> transports, GatewayInbox inbox) : IHealthCheck
{
    public const string Name = "mesh-gateways";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var all = transports.ToList();
        if (all.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded("No gateway transport is configured (Meshtastic:Mqtt, Meshtastic:Tcp or Meshtastic:Simulator)."));
        }

        var down = all.Where(transport => !transport.IsConnected).Select(transport => $"{transport.Description}: {transport.LastError}").ToList();
        var dropped = inbox.Dropped > 0 ? $" {inbox.Dropped} inputs dropped (ingest overloaded)." : string.Empty;
        return Task.FromResult(down.Count == 0
            ? HealthCheckResult.Healthy($"{all.Count} transport(s) connected.{dropped}")
            : HealthCheckResult.Degraded(string.Join("; ", down) + dropped));
    }
}
