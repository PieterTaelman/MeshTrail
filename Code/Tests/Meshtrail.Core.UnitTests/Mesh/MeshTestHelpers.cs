using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Framing;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Shared radio-level test data: the real gateway node and frame builders.</summary>
internal static class MeshTestHelpers
{
    /// <summary>The project's real gateway node (M5Stack C6L "Node 2109").</summary>
    public const uint GatewayNodeNum = 4044729068;
    public const string GatewayNodeId = "!f115aaec";

    /// <summary>
    /// Contact link for the gateway as the Meshtastic app encodes it (base64url, no padding).
    /// Built by hand from the protobuf wire format, so it does not depend on our own encoder.
    /// Public key = bytes 1..32.
    /// </summary>
    public const string GatewayContactUrl =
        "https://meshtastic.org/v/#COzV1ogPEkAKCSFmMTE1YWFlYxIJTm9kZSAyMTA5GgQyMTA5KG9CIAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8g";

    public static byte[] GatewayPublicKey => [.. Enumerable.Range(1, 32).Select(i => (byte)i)];

    public static SharedContact GatewayContact() => new()
    {
        NodeNum = GatewayNodeNum,
        User = new User
        {
            Id = GatewayNodeId,
            LongName = "Node 2109",
            ShortName = "2109",
            HwModel = HardwareModel.M5StackC6L,
            PublicKey = ByteString.CopyFrom(GatewayPublicKey),
        },
    };

    /// <summary>A small FromRadio frame (header + payload) for framing tests.</summary>
    public static byte[] Frame(uint configCompleteId) => FrameWriter.Encode(new FromRadio { ConfigCompleteId = configCompleteId });

    public static uint ConfigCompleteId(byte[] payload) => FromRadio.Parser.ParseFrom(payload).ConfigCompleteId;
}
