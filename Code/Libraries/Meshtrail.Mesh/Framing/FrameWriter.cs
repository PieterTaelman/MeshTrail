using Google.Protobuf;

namespace Meshtrail.Mesh.Framing;

/// <summary>Wraps a protobuf message in the 4-byte stream header.</summary>
public static class FrameWriter
{
    public static byte[] Encode(IMessage message)
    {
        var size = message.CalculateSize();
        if (size > MeshFrame.MaxPayloadLength)
        {
            throw new ArgumentException($"A frame payload can be at most {MeshFrame.MaxPayloadLength} bytes, this one is {size}.", nameof(message));
        }

        var frame = new byte[MeshFrame.HeaderLength + size];
        frame[0] = MeshFrame.Start1;
        frame[1] = MeshFrame.Start2;
        frame[2] = (byte)(size >> 8);
        frame[3] = (byte)size;
        message.WriteTo(frame.AsSpan(MeshFrame.HeaderLength));
        return frame;
    }
}
