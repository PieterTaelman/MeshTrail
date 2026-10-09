using System.Collections.Concurrent;
using System.Threading.Channels;
using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// The way out to the mesh. Every gateway gets its own queue and rate limit (each gateway has its own duty cycle),
/// started on first use. A queue sends through the transport that owns the gateway: MQTT (via its broker), TCP or
/// the simulator. Singleton; handlers see it as <see cref="IMeshOutbox"/>.
/// </summary>
public sealed partial class MeshOutbox(
    IEnumerable<IGatewayTransport> transports,
    IServiceScopeFactory scopeFactory,
    IOptions<MeshOutboundOptions> options,
    TimeProvider timeProvider,
    ILogger<MeshOutbox> logger) : IMeshOutbox, IDisposable
{
    /// <summary>Requests waiting per gateway; when full the oldest is dropped (a text stays Queued in the database).</summary>
    public const int QueueCapacity = 100;

    private readonly MeshOutboundOptions _options = options.Value;
    private readonly IReadOnlyList<IGatewayTransport> _transports = [.. transports];
    private readonly ConcurrentDictionary<uint, Channel<MeshOutboundRequest>> _queues = new();

    /// <summary>
    /// Text messages waiting in a queue (per gateway: a team message goes out through several), so a re-queue after a
    /// reconnect does not send them twice.
    /// </summary>
    private readonly ConcurrentDictionary<(Guid MessageId, uint GatewayNodeNum), byte> _queuedMessages = new();
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;

    public uint VirtualNodeNum => _options.VirtualNodeNum;

    public uint NewPacketId() => MeshPackets.NewPacketId();

    public void Enqueue(MeshOutboundRequest request)
    {
        if (request is TextMessageRequest text && !_queuedMessages.TryAdd((text.MessageId, text.Via.GatewayNodeNum), 0))
        {
            return;
        }

        var queue = _queues.GetOrAdd(request.Via.GatewayNodeNum, _ => StartQueue());
        queue.Writer.TryWrite(request);
    }

    /// <summary>Stops the queues. Safe to call twice (DI disposes it once per registration).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stopping.Cancel();
        _stopping.Dispose();
    }

    private Channel<MeshOutboundRequest> StartQueue()
    {
        var queue = System.Threading.Channels.Channel.CreateBounded<MeshOutboundRequest>(
            new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            Complete);
        _ = Task.Run(() => RunQueueAsync(queue.Reader, _stopping.Token));
        return queue;
    }

    private async Task RunQueueAsync(ChannelReader<MeshOutboundRequest> queue, CancellationToken stoppingToken)
    {
        var rateLimiter = new OutboundRateLimiter(_options.MinInterval, timeProvider);
        try
        {
            await foreach (var request in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(request, rateLimiter, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A text message stays Queued in the database and is queued again when its gateway is back online.
                    LogSendFailed(exception, request.GetType().Name, request.Via.GatewayNodeNum);
                }
                finally
                {
                    Complete(request);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task SendAsync(MeshOutboundRequest request, OutboundRateLimiter rateLimiter, CancellationToken cancellationToken)
    {
        var via = request.Via;
        var transport = FindTransport(via)
            ?? throw new InvalidOperationException($"No {via.Transport} transport for gateway {via.GatewayNodeNum} (broker {via.Broker}).");

        if (request is AddContactRequest contact)
        {
            // Admin message to a TCP gateway itself: it never goes on the air, so no duty-cycle wait.
            await transport.SendAsync(
                via,
                MeshPackets.AddContact(via.GatewayNodeNum, contact.NodeNum, contact.LongName, contact.ShortName, contact.PublicKey, contact.PacketId),
                cancellationToken);
            LogSent(request.GetType().Name, request.NodeNum, request.PacketId, via.GatewayNodeNum);
            return;
        }

        await rateLimiter.WaitTurnAsync(cancellationToken);
        var packet = ToPacket(request);
        if (via.Transport != GatewayTransport.Tcp)
        {
            // Over MQTT the gateway transmits it as if our virtual node sent it.
            packet = MeshPackets.ForDownlink(packet, _options.VirtualNodeNum, _options.HopLimit);
        }

        await transport.SendAsync(via, packet, cancellationToken);
        LogSent(request.GetType().Name, request.NodeNum, request.PacketId, via.GatewayNodeNum);

        if (request is TextMessageRequest text)
        {
            await MarkSentAsync(text.MessageId, cancellationToken);
        }
    }

    private static MeshPacket ToPacket(MeshOutboundRequest request) => request switch
    {
        PositionRequest position => MeshPackets.PositionRequest(position.NodeNum, position.PacketId),
        TracerouteRequest traceroute => MeshPackets.Traceroute(traceroute.NodeNum, traceroute.PacketId),
        TextMessageRequest text => MeshPackets.Text(text.NodeNum, (uint)text.Channel, text.Text, text.PacketId),
        _ => throw new NotSupportedException($"Unknown outbound request {request.GetType().Name}."),
    };

    /// <summary>The transport of the gateway's kind; for MQTT the one of its broker (falls back to the only MQTT transport).</summary>
    private IGatewayTransport? FindTransport(GatewayRoute via)
    {
        var candidates = _transports.Where(transport => transport.Kind == via.Transport).ToList();
        return via.Transport != GatewayTransport.Mqtt
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(transport => transport.Broker == via.Broker) ?? (candidates.Count == 1 ? candidates[0] : null);
    }

    private async Task MarkSentAsync(Guid messageId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new MarkMessageSentCommand(messageId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogMarkSentFailed(exception, messageId);
        }
    }

    /// <summary>The outbox is done with this request (sent, failed or dropped).</summary>
    private void Complete(MeshOutboundRequest request)
    {
        if (request is TextMessageRequest text)
        {
            _queuedMessages.TryRemove((text.MessageId, text.Via.GatewayNodeNum), out _);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent {Request} to node {NodeNum} (packet {PacketId}) via gateway {GatewayNodeNum}")]
    private partial void LogSent(string request, uint nodeNum, uint packetId, uint gatewayNodeNum);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending {Request} via gateway {GatewayNodeNum} failed")]
    private partial void LogSendFailed(Exception exception, string request, uint gatewayNodeNum);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not mark message {MessageId} as sent")]
    private partial void LogMarkSentFailed(Exception exception, Guid messageId);
}
