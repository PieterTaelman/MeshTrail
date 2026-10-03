using System.Threading.Channels;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Outbound;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Shared state between handlers (which queue work) and <see cref="MeshGatewayWorker"/> (which owns the radio).
/// Singleton. Handlers see it as <see cref="IMeshGateway"/>.
/// </summary>
public sealed class MeshGatewayService : IMeshGateway
{
    private readonly Channel<MeshOutboundRequest> _outbound = Channel.CreateBounded<MeshOutboundRequest>(
        // Bounded so a long offline period cannot pile up thousands of stale requests; the oldest are dropped.
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly Lock _lock = new();
    private CancellationTokenSource _reconnect = new();

    /// <summary>Connection state as the worker last saw it; read by the health check without touching the database.</summary>
    public GatewayStatus Status { get; private set; } = GatewayStatus.Offline;

    public string? LastError { get; private set; } = "Not started yet.";

    internal ChannelReader<MeshOutboundRequest> Outbound => _outbound.Reader;

    public uint NewPacketId() => MeshPackets.NewPacketId();

    public void Enqueue(MeshOutboundRequest request) => _outbound.Writer.TryWrite(request);

    public void RequestReconnect()
    {
        lock (_lock)
        {
            _reconnect.Cancel();
        }
    }

    /// <summary>A token that is cancelled when someone asks for a reconnect. Call once per connection attempt.</summary>
    internal CancellationToken NextReconnectToken()
    {
        lock (_lock)
        {
            if (_reconnect.IsCancellationRequested)
            {
                _reconnect.Dispose();
                _reconnect = new CancellationTokenSource();
            }

            return _reconnect.Token;
        }
    }

    internal void SetStatus(GatewayStatus status, string? error)
    {
        Status = status;
        LastError = status == GatewayStatus.Online ? null : error;
    }
}
