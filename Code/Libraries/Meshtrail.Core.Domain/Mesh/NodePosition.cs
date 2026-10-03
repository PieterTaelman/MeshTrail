namespace Meshtrail.Core.Domain.Mesh;

/// <summary>One point of a node's position history (used for trails). Written once, never changed.</summary>
public sealed class NodePosition
{
    private NodePosition()
    {
    }

    public Guid Id { get; private set; }

    public uint NodeNum { get; private set; }

    public GeoPosition Position { get; private set; } = null!;

    /// <summary>When our gateway received it (Position.Time is when the node took the fix).</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    public static NodePosition Record(uint nodeNum, GeoPosition position, DateTimeOffset receivedAt) => new()
    {
        Id = Guid.CreateVersion7(receivedAt),
        NodeNum = nodeNum,
        Position = position,
        ReceivedAt = receivedAt,
    };

    public static NodePosition Rehydrate(Guid id, uint nodeNum, GeoPosition position, DateTimeOffset receivedAt) => new()
    {
        Id = id,
        NodeNum = nodeNum,
        Position = position,
        ReceivedAt = receivedAt,
    };
}
