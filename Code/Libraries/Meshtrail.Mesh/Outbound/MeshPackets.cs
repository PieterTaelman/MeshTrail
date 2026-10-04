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

    /// <summary>
    /// A text message with want_ack, so the mesh reports delivery (ROUTING_APP with our id as request_id).
    /// To = a node (direct message; the firmware encrypts it with that node's public key when it knows it)
    /// or <see cref="NodeIds.Broadcast"/> for everyone on <paramref name="channel"/>.
    /// </summary>
    public static ToRadio Text(uint to, uint channel, string text, uint packetId) =>
        Packet(to, packetId, PortNum.TextMessageApp, ByteString.CopyFromUtf8(text), wantResponse: false, wantAck: true, channel);

    /// <summary>
    /// Tells our own gateway about a contact (node number, names, public key), like scanning a QR code in the app.
    /// Afterwards the gateway can encrypt direct messages to that node. Local admin message: not sent over the air.
    /// </summary>
    public static ToRadio AddContact(uint gatewayNodeNum, uint nodeNum, string longName, string shortName, byte[] publicKey, uint packetId)
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

    private static ToRadio Packet(uint to, uint packetId, PortNum port, ByteString payload, bool wantResponse, bool wantAck = false, uint channel = 0) => new()
    {
        Packet = new MeshPacket
        {
            To = to,
            Id = packetId,
            // From stays 0 so the gateway fills in its own number.
            Channel = channel,
            WantAck = wantAck,
            Decoded = new Data { Portnum = port, Payload = payload, WantResponse = wantResponse },
        },
    };
}
