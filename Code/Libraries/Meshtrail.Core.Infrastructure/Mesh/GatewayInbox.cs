using System.Threading.Channels;
using Meshtastic.Protobufs;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Which gateway something came from. NodeNum is null while a TCP gateway has not said who it is yet.</summary>
public sealed record GatewaySource(GatewayTransport Transport, uint? NodeNum, string? MqttUserName, string? Broker);

/// <summary>Something a transport received.</summary>
public abstract record GatewayInput(DateTimeOffset ReceivedAt);

/// <summary>A message from a gateway: a mesh packet, or (TCP) part of the node's configuration dump. Channel/Root come from the MQTT topic.</summary>
public sealed record GatewayPacketInput(GatewaySource Source, string? Channel, string? Root, FromRadio Message, DateTimeOffset ReceivedAt)
    : GatewayInput(ReceivedAt);

/// <summary>A TCP or simulated gateway connected or lost its connection.</summary>
public sealed record GatewayConnectionInput(GatewaySource Source, bool Connected, string? Error, DateTimeOffset ReceivedAt)
    : GatewayInput(ReceivedAt);

/// <summary>A broker's list of connected gateway logins.</summary>
public sealed record BrokerConnectionsInput(string Broker, IReadOnlyList<string> UserNames, DateTimeOffset ReceivedAt)
    : GatewayInput(ReceivedAt);

/// <summary>
/// The queue between the transports (many writers) and the ingest service (one reader). Bounded, so a flood cannot
/// eat all memory: when full, the oldest input is dropped and counted.
/// </summary>
public sealed class GatewayInbox
{
    public const int Capacity = 20_000;

    private readonly Channel<GatewayInput> _inputs;
    private long _dropped;

    public GatewayInbox()
    {
        _inputs = System.Threading.Channels.Channel.CreateBounded<GatewayInput>(
            new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>Inputs dropped because the ingest could not keep up.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    public ChannelReader<GatewayInput> Reader => _inputs.Reader;

    public void Post(GatewayInput input) => _inputs.Writer.TryWrite(input);
}
