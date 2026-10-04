using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

internal static class MeshMessaging
{
    /// <summary>Destination address on the air: the node, or the broadcast address for channel messages.</summary>
    public const uint BroadcastNodeNum = uint.MaxValue;

    public static TextMessageRequest ToRequest(MeshMessage message) =>
        new(message.ToNodeNum ?? BroadcastNodeNum, message.PacketId, message.Id, message.ChannelIndex, message.Text);
}
