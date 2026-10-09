using System.Security.Cryptography;
using Google.Protobuf;
using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Outbound;

/// <summary>
/// Builds the packets we send. We choose the packet id ourselves (the firmware keeps a non-zero id), so we can match
/// the answer (request_id) to the request before it is even sent. From stays 0 here: over TCP the gateway fills in
/// its own number; over MQTT <see cref="ForDownlink"/> sets our virtual node number.
/// </summary>
public static class MeshPackets
{
    /// <summary>The Meshtastic default; each relay lowers it by one, so the packet travels at most this many hops.</summary>
    public const uint DefaultHopLimit = 3;

    /// <summary>The firmware never accepts more than this.</summary>
    public const uint MaxHopLimit = 7;

    /// <summary>A random, non-zero packet id, like the firmware generates.</summary>
    public static uint NewPacketId() => (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);

    /// <summary>Asks a node to send its current position back (it answers on POSITION_APP).</summary>
    public static MeshPacket PositionRequest(uint to, uint packetId) =>
        Packet(to, packetId, PortNum.PositionApp, new Position().ToByteString(), wantResponse: true);

    /// <summary>Asks a node for the route between us; every relay adds itself (answer on TRACEROUTE_APP).</summary>
    public static MeshPacket Traceroute(uint to, uint packetId) =>
        Packet(to, packetId, PortNum.TracerouteApp, new RouteDiscovery().ToByteString(), wantResponse: true);

    /// <summary>
    /// A text message with want_ack, so the mesh reports delivery (ROUTING_APP with our id as request_id).
    /// To = a node (direct message) or <see cref="NodeIds.Broadcast"/> for everyone on <paramref name="channel"/>.
    /// </summary>
    public static MeshPacket Text(uint to, uint channel, string text, uint packetId) =>
        Packet(to, packetId, PortNum.TextMessageApp, ByteString.CopyFromUtf8(text), wantResponse: false, wantAck: true, channel);

    /// <summary>
    /// Tells a TCP gateway about a contact (node number, names, public key), like scanning a QR code in the app.
    /// Afterwards the gateway can encrypt direct messages to that node. Local admin message: not sent over the air.
    /// </summary>
    public static MeshPacket AddContact(uint gatewayNodeNum, uint nodeNum, string longName, string shortName, byte[] publicKey, uint packetId)
    {
        var admin = new AdminMessage
        {
            AddContact = new SharedContact
            {
                NodeNum = nodeNum,
                User = new User
                {
                    Id = NodeIds.Format(nodeNum),
                    LongName = longName,
                    ShortName = shortName,
                    PublicKey = ByteString.CopyFrom(publicKey),
                },
                // We verified ownership with a code sent to the node, so the key is trusted.
                ManuallyVerified = true,
            },
        };
        return Packet(gatewayNodeNum, packetId, PortNum.AdminApp, admin.ToByteString(), wantResponse: false);
    }

    /// <summary>
    /// Prepares a packet for an MQTT downlink: the gateway transmits it as if <paramref name="from"/> sent it. It must
    /// not be the gateway's own number (the firmware drops "its own" packets coming back from MQTT).
    /// </summary>
    public static MeshPacket ForDownlink(MeshPacket packet, uint from, uint hopLimit)
    {
        var limit = Math.Clamp(hopLimit, 1, MaxHopLimit);
        var copy = packet.Clone();
        copy.From = from;
        copy.HopLimit = limit;
        copy.HopStart = limit;
        return copy;
    }

    /// <summary>Wraps a packet for the TCP API.</summary>
    public static ToRadio ToRadio(MeshPacket packet) => new() { Packet = packet };

    private static MeshPacket Packet(uint to, uint packetId, PortNum port, ByteString payload, bool wantResponse, bool wantAck = false, uint channel = 0) => new()
    {
        To = to,
        Id = packetId,
        Channel = channel,
        WantAck = wantAck,
        Decoded = new Data { Portnum = port, Payload = payload, WantResponse = wantResponse },
    };
}
