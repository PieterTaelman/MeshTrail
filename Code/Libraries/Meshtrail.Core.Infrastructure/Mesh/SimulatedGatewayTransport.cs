using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Simulation;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>The simulator's gateways as a transport: they behave like MQTT gateways (packets from our virtual node).</summary>
public sealed class SimulatedGatewayTransport(SimulatedMesh mesh, TimeProvider timeProvider) : IGatewayTransport
{
    public GatewayTransport Kind => GatewayTransport.Simulated;

    public string? Broker => null;

    public string Description => "simulator";

    public bool IsConnected { get; private set; }

    public string? LastError => IsConnected ? null : "Not started yet.";

    /// <summary>The fake mesh, e.g. to raise an SOS from a development tool.</summary>
    public SimulatedMesh Mesh => mesh;

    public async Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken)
    {
        foreach (var gateway in mesh.Gateways)
        {
            inbox.Post(new GatewayConnectionInput(Source(gateway.NodeNum), true, null, timeProvider.GetUtcNow()));
        }

        IsConnected = true;
        await mesh.StartAsync();
        try
        {
            await foreach (var uplink in mesh.ReadAllAsync(stoppingToken))
            {
                inbox.Post(new GatewayPacketInput(
                    Source(uplink.GatewayNodeNum), uplink.Channel, null, new FromRadio { Packet = uplink.Packet }, timeProvider.GetUtcNow()));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        finally
        {
            IsConnected = false;
        }
    }

    public Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken) =>
        mesh.SendAsync(via.GatewayNodeNum, packet, cancellationToken);

    private static GatewaySource Source(uint gatewayNodeNum) => new(GatewayTransport.Simulated, gatewayNodeNum, null, null);
}
