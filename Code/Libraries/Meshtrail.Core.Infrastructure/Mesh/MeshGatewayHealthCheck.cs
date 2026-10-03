using Meshtrail.Core.Domain.Mesh;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Reports the gateway connection. Never "Unhealthy": the API works without the radio, so an offline gateway is
/// only "Degraded" and /health/ready keeps answering 200.
/// </summary>
public sealed class MeshGatewayHealthCheck(MeshGatewayService gateway) : IHealthCheck
{
    public const string Name = "mesh-gateway";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(gateway.Status == GatewayStatus.Online
            ? HealthCheckResult.Healthy("Gateway online.")
            : HealthCheckResult.Degraded($"Gateway {gateway.Status.ToString().ToLowerInvariant()}: {gateway.LastError}"));
}
