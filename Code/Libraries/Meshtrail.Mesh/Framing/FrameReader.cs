namespace Meshtrail.Mesh.Framing;

/// <summary>
/// Cuts a byte stream into frame payloads. TCP delivers bytes in arbitrary chunks, so a frame can be split over
/// several reads (or several frames can arrive in one read); this class keeps the leftovers between calls.
/// Not thread-safe: use one instance per connection, from one reader loop.
/// </summary>
public sealed class FrameReader
{
    private byte[] _buffer = new byte[MeshFrame.HeaderLength + MeshFrame.MaxPayloadLength];
    private int _count;

    /// <summary>Bytes thrown away while looking for a frame start (debug text, corrupt frames). Handy for logging.</summary>
    public long DiscardedBytes { get; private set; }

    /// <summary>Adds received bytes and returns every complete payload they finish, in order.</summary>
    public IReadOnlyList<byte[]> Push(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_count + data.Length);
        data.CopyTo(_buffer.AsSpan(_count));
        _count += data.Length;

        var payloads = new List<byte[]>();
        var position = 0;

        while (true)
        {
            var start = FindStart(position);
            if (start < 0)
            {
                // Keep a trailing 0x94: it may be the first half of the next start marker.
                var keepFrom = _count > position && _buffer[_count - 1] == MeshFrame.Start1 ? _count - 1 : _count;
                DiscardedBytes += keepFrom - position;
                position = keepFrom;
                break;
            }

            DiscardedBytes += start - position;
            position = start;

            if (_count - position < MeshFrame.HeaderLength)
            {
                break;
            }

            var length = (_buffer[position + 2] << 8) | _buffer[position + 3];
            if (length > MeshFrame.MaxPayloadLength)
            {
                // Not a real frame: skip this start byte and look for the next marker.
                DiscardedBytes++;
                position++;
                continue;
            }

            if (_count - position < MeshFrame.HeaderLength + length)
            {
                break;
            }

            payloads.Add(_buffer.AsSpan(position + MeshFrame.HeaderLength, length).ToArray());
            position += MeshFrame.HeaderLength + length;
        }

        Compact(position);
        return payloads;
    }

    private int FindStart(int from)
    {
        for (var i = from; i < _count - 1; i++)
        {
            if (_buffer[i] == MeshFrame.Start1 && _buffer[i + 1] == MeshFrame.Start2)
            {
                return i;
            }
        }

        return -1;
    }

    private void Compact(int consumed)
    {
        if (consumed == 0)
        {
            return;
        }

        _buffer.AsSpan(consumed, _count - consumed).CopyTo(_buffer);
        _count -= consumed;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
        }
    }
}
