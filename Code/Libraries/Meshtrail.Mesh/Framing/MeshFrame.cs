namespace Meshtrail.Mesh.Framing;

/// <summary>
/// Constants of the Meshtastic stream protocol (TCP and serial): 0x94 0xC3, a 2-byte big-endian length, then a protobuf.
/// </summary>
public static class MeshFrame
{
    public const byte Start1 = 0x94;
    public const byte Start2 = 0xC3;
    public const int HeaderLength = 4;

    /// <summary>Firmware never sends more than this; a bigger length means we are reading garbage.</summary>
    public const int MaxPayloadLength = 512;
}
