using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// One way gateways reach us: the Meshtrail MQTT broker (one transport per broker/region), a local TCP node, or the
/// simulator. A transport posts what it receives to the <see cref="GatewayInbox"/> and sends packets to a gateway.
/// </summary>
public interface IGatewayTransport
{
    GatewayTransport Kind { get; }

    /// <summary>MQTT: the broker (region) this transport is logged in to. Null for TCP and the simulator.</summary>
    string? Broker { get; }

    /// <summary>Human-readable target, safe to log (never contains a password).</summary>
    string Description { get; }

    bool IsConnected { get; }

    string? LastError { get; }

    /// <summary>Connects, reconnects with growing pauses and posts everything it receives. Never throws; ends on shutdown.</summary>
    Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken);

    /// <summary>Hands one packet to the gateway in <paramref name="via"/>. Throws when the transport is not connected.</summary>
    Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken);
}
