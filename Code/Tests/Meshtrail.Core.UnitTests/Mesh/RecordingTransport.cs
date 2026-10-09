using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>A transport that only records what the outbox sends through it.</summary>
internal sealed class RecordingTransport(GatewayTransport kind, string? broker) : IGatewayTransport
{
    private readonly List<(GatewayRoute Via, MeshPacket Packet)> _sent = [];
    private readonly Lock _lock = new();

    public GatewayTransport Kind => kind;

    public string? Broker => broker;

    public string Description => "recording";

    public bool IsConnected => true;

    public string? LastError => null;

    public IReadOnlyList<(GatewayRoute Via, MeshPacket Packet)> Sent
    {
        get
        {
            lock (_lock)
            {
                return [.. _sent];
            }
        }
    }

    public Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken) => Task.CompletedTask;

    public Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _sent.Add((via, packet));
        }

        return Task.CompletedTask;
    }

    /// <summary>Waits (max 5 s) until at least <paramref name="count"/> packets were sent.</summary>
    public async Task<IReadOnlyList<(GatewayRoute Via, MeshPacket Packet)>> WaitForAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Sent.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        return Sent;
    }
}
