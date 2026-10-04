namespace Meshtrail.Core.Application.Abstractions;

/// <summary>Something we want the gateway to put on the air. PacketId lets us match the answer later.</summary>
public abstract record MeshOutboundRequest(uint NodeNum, uint PacketId);

/// <summary>Ask a node to send its current position.</summary>
public sealed record PositionRequest(uint NodeNum, uint PacketId) : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>Ask for the route (list of relays) between our gateway and a node.</summary>
public sealed record TracerouteRequest(uint NodeNum, uint PacketId) : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>
/// Send a stored text message (MessageId). NodeNum = destination node or 0xFFFFFFFF (broadcast on Channel).
/// The gateway reports back with MarkMessageSent once it is on the air.
/// </summary>
public sealed record TextMessageRequest(uint NodeNum, uint PacketId, Guid MessageId, int Channel, string Text)
    : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>Give our gateway a verified contact (name + public key) so it can encrypt direct messages to that node.</summary>
public sealed record AddContactRequest(uint NodeNum, uint PacketId, string LongName, string ShortName, byte[] PublicKey)
    : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>
/// The radio side as handlers see it. Implemented in Infrastructure by the gateway worker, so handlers never touch
/// sockets or protobuf and stay easy to unit test.
/// </summary>
public interface IMeshGateway
{
    /// <summary>A new random packet id (non-zero), so the answer can be matched to the request.</summary>
    uint NewPacketId();

    /// <summary>
    /// Queues a request; the gateway sends it as soon as the duty-cycle rate limit allows.
    /// A text message that is already queued (same MessageId) is not queued twice.
    /// </summary>
    void Enqueue(MeshOutboundRequest request);

    /// <summary>Drops the current connection (if any) and connects again right away.</summary>
    void RequestReconnect();
}
