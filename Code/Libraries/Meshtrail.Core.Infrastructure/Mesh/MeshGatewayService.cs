using System.Collections.Concurrent;
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
    /// <summary>Text messages waiting in the queue, so a re-queue after reconnect does not send them twice.</summary>
    private readonly ConcurrentDictionary<Guid, byte> _queuedMessages = new();
    private readonly Channel<MeshOutboundRequest> _outbound;
    private readonly Lock _lock = new();
    private CancellationTokenSource _reconnect = new();

    public MeshGatewayService()
    {
        // Bounded so a long offline period cannot pile up thousands of requests; the oldest are dropped.
        // A dropped text message stays Queued in the database and is queued again on the next connect.
        _outbound = Channel.CreateBounded<MeshOutboundRequest>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            dropped => Complete(dropped));
    }

    /// <summary>Connection state as the worker last saw it; read by the health check without touching the database.</summary>
    public GatewayStatus Status { get; private set; } = GatewayStatus.Offline;

    public string? LastError { get; private set; } = "Not started yet.";

    /// <summary>Our gateway's own node number, once it told us (needed to address admin messages to it).</summary>
    public uint? GatewayNodeNum { get; internal set; }

    internal ChannelReader<MeshOutboundRequest> Outbound => _outbound.Reader;

    public uint NewPacketId() => MeshPackets.NewPacketId();

    public void Enqueue(MeshOutboundRequest request)
    {
        if (request is TextMessageRequest text && !_queuedMessages.TryAdd(text.MessageId, 0))
        {
            return;
        }

        _outbound.Writer.TryWrite(request);
    }

    public void RequestReconnect()
    {
        lock (_lock)
        {
            _reconnect.Cancel();
        }
    }

    /// <summary>The worker is done with this request (sent, failed or dropped).</summary>
    internal void Complete(MeshOutboundRequest request)
    {
        if (request is TextMessageRequest text)
        {
            _queuedMessages.TryRemove(text.MessageId, out _);
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
