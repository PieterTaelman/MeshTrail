using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Gateways.Events;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways;

/// <summary>Builds gateway DTOs: a gateway's name and position come from its node, its channels from the channel table.</summary>
internal static class GatewayViews
{
    public const string PendingName = "New gateway (waiting for its first uplink)";

    public static async Task<IReadOnlyList<GatewayDto>> ToDtosAsync(
        IReadOnlyList<MeshGateway> list,
        IMeshNodeRepository nodes,
        IMeshGatewayRepository gateways,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        if (list.Count == 0)
        {
            return [];
        }

        var nodeNums = list.Where(gateway => gateway.NodeNum is not null).Select(gateway => gateway.NodeNum!.Value).Distinct().ToList();
        var known = (await nodes.GetManyAsync(nodeNums, cancellationToken)).ToDictionary(node => node.NodeNum);
        var channels = await gateways.GetChannelsAsync([.. list.Select(gateway => gateway.Id)], cancellationToken);

        return [.. list.Select(gateway => gateway.ToDto(
            gateway.NodeNum is { } nodeNum && known.TryGetValue(nodeNum, out var node) ? node : null,
            channels.TryGetValue(gateway.Id, out var names) ? names : [],
            currentUserId))];
    }

    /// <summary>Tells every open browser that a gateway changed (no personal fields: IsMine is always false here).</summary>
    public static async Task PublishAsync(
        MeshGateway gateway,
        IMeshNodeRepository nodes,
        IMeshGatewayRepository gateways,
        IPublisher publisher,
        CancellationToken cancellationToken)
    {
        var dto = (await ToDtosAsync([gateway], nodes, gateways, null, cancellationToken))[0];
        await publisher.Publish(new GatewayStatusChangedNotification(dto), cancellationToken);
    }

    public static GatewayDto ToDto(this MeshGateway gateway, MeshNode? node, IReadOnlyList<string> channels, string? currentUserId)
    {
        var isMine = currentUserId is not null && gateway.IsOwnedBy(currentUserId);
        return new GatewayDto(
            gateway.Id,
            gateway.NodeNum,
            gateway.NodeNum is { } nodeNum ? MeshNode.FormatNodeId(nodeNum) : null,
            node?.LongName ?? (gateway.NodeNum is { } num ? MeshNode.FormatNodeId(num) : PendingName),
            gateway.Transport.ToString(),
            gateway.Status.ToString(),
            gateway.StatusChangedAt,
            gateway.LastUplinkAt,
            isMine ? gateway.LastError : null,
            gateway.FirmwareVersion,
            gateway.Broker,
            channels,
            isMine,
            isMine ? gateway.MqttUserName : null,
            gateway.CreatedAt,
            node?.LastPosition?.ToDto());
    }
}
