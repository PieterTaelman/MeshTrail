using System.Text;
using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

public enum MessageDirection
{
    Inbound,
    Outbound,
}

public enum MessageKind
{
    /// <summary>A normal chat message.</summary>
    Text,

    /// <summary>Carries a registration code: never shown in the chat or returned with its text.</summary>
    Verification,
}

public enum MessageStatus
{
    /// <summary>Stored, waiting for the gateway (and the duty-cycle rate limit).</summary>
    Queued,

    /// <summary>Handed to the gateway; waiting for the delivery report.</summary>
    Sent,

    /// <summary>The mesh confirmed delivery.</summary>
    Acked,

    /// <summary>The mesh reported an error, or no confirmation came in time.</summary>
    Failed,

    /// <summary>An inbound message.</summary>
    Received,
}

/// <summary>
/// A text message on the mesh. Outbound: Queued → Sent → Acked or Failed. Inbound: Received.
/// ToNodeNum null = broadcast on ChannelIndex; otherwise a direct message.
/// </summary>
public sealed class MeshMessage
{
    /// <summary>Max size of a message we send. LoRa packets are small; 200 bytes leaves room for headers.</summary>
    public const int TextMaxBytes = 200;

    /// <summary>Max length we store for inbound text (the radio cannot carry more than ~233 bytes anyway).</summary>
    public const int StoredTextMaxLength = 256;

    /// <summary>Meshtastic nodes have channels 0 (primary) to 7.</summary>
    public const int MaxChannelIndex = 7;

    public const int FailureReasonMaxLength = 100;

    public const int ChannelNameMaxLength = 30;

    private MeshMessage()
    {
    }

    public Guid Id { get; private set; }

    public MessageDirection Direction { get; private set; }

    public MessageKind Kind { get; private set; }

    public int ChannelIndex { get; private set; }

    /// <summary>Channel name as the gateway reported it (MQTT topic); null when unknown (TCP).</summary>
    public string? ChannelName { get; private set; }

    /// <summary>
    /// The gateway the message went out through (outbound) or first arrived through (inbound). Null for a team message
    /// we sent: it goes out through every gateway that carries the team's channel.
    /// </summary>
    public uint? GatewayNodeNum { get; private set; }

    /// <summary>The team whose channel the message is on (team chat); null for direct messages.</summary>
    public Guid? TeamId { get; private set; }

    /// <summary>Who wrote an outbound message (user id): decides who may read the conversation.</summary>
    public string? CreatedById { get; private set; }

    public uint? FromNodeNum { get; private set; }

    public uint? ToNodeNum { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>Packet id on the air; delivery reports refer to it.</summary>
    public uint PacketId { get; private set; }

    public MessageStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public double? Snr { get; private set; }

    public int? Rssi { get; private set; }

    public int? HopsAway { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? AckedAt { get; private set; }

    public bool IsDirect => ToNodeNum is not null;

    /// <summary>The other node of a direct-message conversation (null for channel messages).</summary>
    public uint? PeerNodeNum => !IsDirect ? null : Direction == MessageDirection.Outbound ? ToNodeNum : FromNodeNum;

    public static int ByteCount(string text) => Encoding.UTF8.GetByteCount(text);

    /// <summary>
    /// A message we want to send through <paramref name="gatewayNodeNum"/> (null = several gateways, team chat).
    /// <paramref name="toNodeNum"/> null = broadcast on the channel. <paramref name="fromNodeNum"/> = the sender address
    /// on the air (our virtual node, or the TCP gateway).
    /// </summary>
    public static MeshMessage QueueOutbound(
        int channelIndex,
        string? channelName,
        uint? toNodeNum,
        string text,
        MessageKind kind,
        uint packetId,
        uint? gatewayNodeNum,
        uint? fromNodeNum,
        string? createdById,
        string createdBy,
        DateTimeOffset now,
        Guid? teamId = null)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException("A message needs text.");
        }

        if (ByteCount(trimmed) > TextMaxBytes)
        {
            throw new DomainException($"A message can be at most {TextMaxBytes} bytes (emoji and accents count double or more).");
        }

        if (channelIndex is < 0 or > MaxChannelIndex)
        {
            throw new DomainException($"The channel must be between 0 and {MaxChannelIndex}.");
        }

        if (packetId == 0)
        {
            throw new DomainException("A message needs a packet id.");
        }

        return new MeshMessage
        {
            Id = Guid.CreateVersion7(now),
            Direction = MessageDirection.Outbound,
            Kind = kind,
            ChannelIndex = channelIndex,
            ChannelName = UntrustedText.Clean(channelName, ChannelNameMaxLength),
            GatewayNodeNum = gatewayNodeNum,
            FromNodeNum = fromNodeNum,
            ToNodeNum = toNodeNum,
            Text = trimmed,
            PacketId = packetId,
            Status = MessageStatus.Queued,
            CreatedAt = now,
            CreatedById = createdById,
            CreatedBy = createdBy,
            TeamId = teamId,
        };
    }

    /// <summary>A message a gateway received. The text and channel name are untrusted: they are cleaned and cut.</summary>
    public static MeshMessage Received(
        uint fromNodeNum,
        uint? toNodeNum,
        int channelIndex,
        string? channelName,
        uint? gatewayNodeNum,
        string text,
        uint packetId,
        double? snr,
        int? rssi,
        int? hopsAway,
        DateTimeOffset receivedAt,
        Guid? teamId = null) => new()
    {
        Id = Guid.CreateVersion7(receivedAt),
        Direction = MessageDirection.Inbound,
        Kind = MessageKind.Text,
        ChannelIndex = Math.Clamp(channelIndex, 0, MaxChannelIndex),
        ChannelName = UntrustedText.Clean(channelName, ChannelNameMaxLength),
        GatewayNodeNum = gatewayNodeNum,
        FromNodeNum = fromNodeNum,
        ToNodeNum = toNodeNum,
        Text = UntrustedText.Clean(text, StoredTextMaxLength) ?? string.Empty,
        PacketId = packetId,
        Status = MessageStatus.Received,
        Snr = snr,
        Rssi = rssi,
        HopsAway = hopsAway,
        CreatedAt = receivedAt,
        TeamId = teamId,
    };

    public static MeshMessage Rehydrate(
        Guid id,
        MessageDirection direction,
        MessageKind kind,
        int channelIndex,
        string? channelName,
        uint? gatewayNodeNum,
        uint? fromNodeNum,
        uint? toNodeNum,
        string text,
        uint packetId,
        MessageStatus status,
        string? failureReason,
        double? snr,
        int? rssi,
        int? hopsAway,
        DateTimeOffset createdAt,
        string? createdById,
        string? createdBy,
        DateTimeOffset? sentAt,
        DateTimeOffset? ackedAt,
        Guid? teamId) => new()
    {
        Id = id,
        Direction = direction,
        Kind = kind,
        ChannelIndex = channelIndex,
        ChannelName = channelName,
        GatewayNodeNum = gatewayNodeNum,
        FromNodeNum = fromNodeNum,
        ToNodeNum = toNodeNum,
        Text = text,
        PacketId = packetId,
        Status = status,
        FailureReason = failureReason,
        Snr = snr,
        Rssi = rssi,
        HopsAway = hopsAway,
        CreatedAt = createdAt,
        CreatedById = createdById,
        CreatedBy = createdBy,
        SentAt = sentAt,
        AckedAt = ackedAt,
        TeamId = teamId,
    };

    public void MarkSent(DateTimeOffset now)
    {
        EnsureStatus(MessageStatus.Queued, "sent");
        Status = MessageStatus.Sent;
        SentAt = now;
    }

    public void MarkAcked(DateTimeOffset now)
    {
        EnsureStatus(MessageStatus.Sent, "acknowledged");
        Status = MessageStatus.Acked;
        AckedAt = now;
    }

    public void MarkFailed(string reason)
    {
        if (Status is not (MessageStatus.Queued or MessageStatus.Sent))
        {
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} message cannot fail.");
        }

        Status = MessageStatus.Failed;
        FailureReason = reason.Length > FailureReasonMaxLength ? reason[..FailureReasonMaxLength] : reason;
    }

    private void EnsureStatus(MessageStatus expected, string action)
    {
        if (Direction != MessageDirection.Outbound || Status != expected)
        {
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} {Direction.ToString().ToLowerInvariant()} message cannot be marked {action}.");
        }
    }
}
