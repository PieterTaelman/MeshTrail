using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

public enum TracerouteStatus
{
    Pending,
    Completed,

    /// <summary>No answer within <see cref="NodeTraceroute.Timeout"/>. Not stored: derived from the request time.</summary>
    TimedOut,
}

/// <summary>
/// A traceroute we asked for: which relays a packet passes between our gateway and the node, and back.
/// The answer is matched by PacketId (the radio echoes it as request_id).
/// </summary>
public sealed class NodeTraceroute
{
    public const int RequestedByMaxLength = 256;

    /// <summary>A route holds at most 8 relays in the protocol; more means the answer is corrupt.</summary>
    public const int MaxHops = 8;

    /// <summary>The firmware gives up retrying long before this; after it we stop waiting.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private NodeTraceroute()
    {
    }

    public Guid Id { get; private set; }

    public uint NodeNum { get; private set; }

    public uint PacketId { get; private set; }

    /// <summary>Stored status: Pending or Completed. Use <see cref="GetStatus"/> to also see time-outs.</summary>
    public TracerouteStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public string RequestedBy { get; private set; } = string.Empty;

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Relays from the gateway towards the node (excluding both ends).</summary>
    public IReadOnlyList<uint> RouteTowards { get; private set; } = [];

    /// <summary>SNR in dB per hop towards the node; null = unknown.</summary>
    public IReadOnlyList<double?> SnrTowards { get; private set; } = [];

    public IReadOnlyList<uint> RouteBack { get; private set; } = [];

    public IReadOnlyList<double?> SnrBack { get; private set; } = [];

    public static NodeTraceroute Request(uint nodeNum, uint packetId, string requestedBy, DateTimeOffset now)
    {
        if (packetId == 0)
        {
            throw new DomainException("A traceroute needs a packet id.");
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new DomainException("Every traceroute must be linked to a user.");
        }

        return new NodeTraceroute
        {
            Id = Guid.CreateVersion7(now),
            NodeNum = nodeNum,
            PacketId = packetId,
            Status = TracerouteStatus.Pending,
            RequestedAt = now,
            RequestedBy = requestedBy.Length > RequestedByMaxLength ? requestedBy[..RequestedByMaxLength] : requestedBy,
        };
    }

    public static NodeTraceroute Rehydrate(
        Guid id,
        uint nodeNum,
        uint packetId,
        TracerouteStatus status,
        DateTimeOffset requestedAt,
        string requestedBy,
        DateTimeOffset? completedAt,
        IReadOnlyList<uint> routeTowards,
        IReadOnlyList<double?> snrTowards,
        IReadOnlyList<uint> routeBack,
        IReadOnlyList<double?> snrBack) => new()
    {
        Id = id,
        NodeNum = nodeNum,
        PacketId = packetId,
        Status = status,
        RequestedAt = requestedAt,
        RequestedBy = requestedBy,
        CompletedAt = completedAt,
        RouteTowards = routeTowards,
        SnrTowards = snrTowards,
        RouteBack = routeBack,
        SnrBack = snrBack,
    };

    public TracerouteStatus GetStatus(DateTimeOffset now) =>
        Status == TracerouteStatus.Pending && now - RequestedAt > Timeout ? TracerouteStatus.TimedOut : Status;

    /// <summary>Stores the answer. Returns false when this traceroute already had one (the radio may repeat it).</summary>
    public bool Complete(
        IReadOnlyList<uint> routeTowards,
        IReadOnlyList<double?> snrTowards,
        IReadOnlyList<uint> routeBack,
        IReadOnlyList<double?> snrBack,
        DateTimeOffset now)
    {
        if (Status == TracerouteStatus.Completed)
        {
            return false;
        }

        // Untrusted input: never store more hops than the protocol allows.
        RouteTowards = [.. routeTowards.Take(MaxHops)];
        SnrTowards = [.. snrTowards.Take(MaxHops + 1)];
        RouteBack = [.. routeBack.Take(MaxHops)];
        SnrBack = [.. snrBack.Take(MaxHops + 1)];
        Status = TracerouteStatus.Completed;
        CompletedAt = now;
        return true;
    }
}
