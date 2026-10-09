using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

internal static class MeshMessaging
{
    /// <summary>Destination address on the air: the node, or the broadcast address for channel messages.</summary>
    public const uint BroadcastNodeNum = uint.MaxValue;

    public static TextMessageRequest ToRequest(MeshMessage message, GatewayRoute via) =>
        new(via, message.ToNodeNum ?? BroadcastNodeNum, message.PacketId, message.Id, message.ChannelIndex, message.Text);

    /// <summary>The sender address our packets carry: the TCP gateway itself, or our virtual node over MQTT/simulator.</summary>
    public static uint SenderFor(this GatewayRoute via, IMeshOutbox outbox) =>
        via.Transport == GatewayTransport.Tcp ? via.GatewayNodeNum : outbox.VirtualNodeNum;

    /// <summary>Hands the gateway's still-Queued messages to the outbox again (after it came back online). Safe to repeat.</summary>
    public static async Task<int> RequeueAsync(IMeshMessageRepository messages, IMeshOutbox outbox, MeshGateway gateway, CancellationToken cancellationToken)
    {
        if (!gateway.CanSend)
        {
            return 0;
        }

        var via = gateway.ToRoute();
        var queued = await messages.GetQueuedAsync(via.GatewayNodeNum, cancellationToken);
        foreach (var message in queued)
        {
            outbox.Enqueue(ToRequest(message, via));
        }

        return queued.Count;
    }
}
