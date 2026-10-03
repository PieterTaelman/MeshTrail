namespace Meshtrail.Core.Application.Abstractions;

/// <summary>Something we want the gateway to put on the air. PacketId lets us match the answer later.</summary>
public abstract record MeshOutboundRequest(uint NodeNum, uint PacketId);

/// <summary>Ask a node to send its current position.</summary>
public sealed record PositionRequest(uint NodeNum, uint PacketId) : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>Ask for the route (list of relays) between our gateway and a node.</summary>
public sealed record TracerouteRequest(uint NodeNum, uint PacketId) : MeshOutboundRequest(NodeNum, PacketId);

/// <summary>
/// The radio side as handlers see it. Implemented in Infrastructure by the gateway worker, so handlers never touch
/// sockets or protobuf and stay easy to unit test.
/// </summary>
public interface IMeshGateway
{
    /// <summary>A new random packet id (non-zero), so the answer can be matched to the request.</summary>
    uint NewPacketId();

    /// <summary>Queues a request; the gateway sends it as soon as the duty-cycle rate limit allows.</summary>
    void Enqueue(MeshOutboundRequest request);

    /// <summary>Drops the current connection (if any) and connects again right away.</summary>
    void RequestReconnect();
}
