using System.Security.Cryptography;
using Google.Protobuf;
using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Outbound;

/// <summary>
/// Builds the ToRadio messages we send. We choose the packet id ourselves (the firmware keeps a non-zero id),
/// so we can match the answer (request_id) to the request before it is even sent.
/// </summary>
public static class MeshPackets
{
    /// <summary>A random, non-zero packet id, like the firmware generates.</summary>
    public static uint NewPacketId() => (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);

    /// <summary>Asks a node to send its current position back (it answers on POSITION_APP).</summary>
    public static ToRadio PositionRequest(uint to, uint packetId) =>
        Packet(to, packetId, PortNum.PositionApp, new Position().ToByteString(), wantResponse: true);

    /// <summary>Asks a node for the route between us; every relay adds itself (answer on TRACEROUTE_APP).</summary>
    public static ToRadio Traceroute(uint to, uint packetId) =>
        Packet(to, packetId, PortNum.TracerouteApp, new RouteDiscovery().ToByteString(), wantResponse: true);

    private static ToRadio Packet(uint to, uint packetId, PortNum port, ByteString payload, bool wantResponse) => new()
    {
        Packet = new MeshPacket
        {
            To = to,
            Id = packetId,
            // Channel 0 = primary channel; From stays 0 so the gateway fills in its own number.
            Channel = 0,
            Decoded = new Data { Portnum = port, Payload = payload, WantResponse = wantResponse },
        },
    };
}
