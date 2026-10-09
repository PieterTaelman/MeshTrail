using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Abstractions;

/// <summary>
/// Which gateway sends a request and how to reach it: over MQTT through its broker (login, root topic, channel), or
/// over the TCP/simulator connection. Built from a <see cref="MeshGateway"/> by the routing helper.
/// </summary>
public sealed record GatewayRoute(
    uint GatewayNodeNum,
    GatewayTransport Transport,
    string? Broker,
    string? MqttUserName,
    string? MqttRoot,
    string? Channel);

/// <summary>Something we want a gateway to put on the air. PacketId lets us match the answer later.</summary>
public abstract record MeshOutboundRequest(GatewayRoute Via, uint NodeNum, uint PacketId);

/// <summary>Ask a node to send its current position.</summary>
public sealed record PositionRequest(GatewayRoute Via, uint NodeNum, uint PacketId) : MeshOutboundRequest(Via, NodeNum, PacketId);

/// <summary>Ask for the route (list of relays) between the gateway and a node.</summary>
public sealed record TracerouteRequest(GatewayRoute Via, uint NodeNum, uint PacketId) : MeshOutboundRequest(Via, NodeNum, PacketId);

/// <summary>
/// Send a stored text message (MessageId). NodeNum = destination node or 0xFFFFFFFF (broadcast on Channel).
/// The outbox reports back with MarkMessageSent once it is on its way.
/// </summary>
public sealed record TextMessageRequest(GatewayRoute Via, uint NodeNum, uint PacketId, Guid MessageId, int Channel, string Text)
    : MeshOutboundRequest(Via, NodeNum, PacketId);

/// <summary>Give a TCP gateway a verified contact (name + public key) so it can encrypt direct messages to that node.</summary>
public sealed record AddContactRequest(GatewayRoute Via, uint NodeNum, uint PacketId, string LongName, string ShortName, byte[] PublicKey)
    : MeshOutboundRequest(Via, NodeNum, PacketId);

/// <summary>
/// The way out to the mesh as handlers see it. Implemented in Infrastructure (one queue and rate limit per gateway),
/// so handlers never touch MQTT, sockets or protobuf and stay easy to unit test.
/// </summary>
public interface IMeshOutbox
{
    /// <summary>
    /// The node number our MQTT/simulator packets carry as sender. Replies and delivery reports come back to it.
    /// (Over TCP the gateway itself is the sender.)
    /// </summary>
    uint VirtualNodeNum { get; }

    /// <summary>A new random packet id (non-zero), so the answer can be matched to the request.</summary>
    uint NewPacketId();

    /// <summary>
    /// Queues a request on its gateway; it is sent as soon as that gateway's rate limit allows.
    /// A text message that is already queued (same MessageId) is not queued twice.
    /// </summary>
    void Enqueue(MeshOutboundRequest request);
}
