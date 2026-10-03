using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>
/// The steps every "something arrived from the radio about node X" handler shares:
/// load the node (or discover it), let the caller change it, save, tell the clients.
/// </summary>
internal static class MeshNodeUpdates
{
    public static async Task<MeshNode> ApplyAsync(
        IMeshNodeRepository nodes,
        IMeshGatewayRepository gateways,
        IPublisher publisher,
        uint nodeNum,
        DateTimeOffset now,
        Func<MeshNode, Task> change,
        CancellationToken cancellationToken)
    {
        var node = await nodes.GetAsync(nodeNum, cancellationToken);
        var isNew = node is null;
        node ??= MeshNode.Discover(nodeNum, now);

        await change(node);

        if (isNew)
        {
            await nodes.AddAsync(node, cancellationToken);
        }
        else
        {
            await nodes.UpdateAsync(node, cancellationToken);
        }

        await nodes.SaveChangesAsync(cancellationToken);

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        await publisher.Publish(new NodeUpdatedNotification(node.ToDto(now, gateway?.NodeNum)), cancellationToken);
        return node;
    }

    /// <summary>Stores a fix on the node and, when it is newer than the last one, in the position history.</summary>
    public static async Task RecordPositionAsync(
        IMeshNodeRepository nodes,
        MeshNode node,
        RadioPosition position,
        DateTimeOffset receivedAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fix = GeoPosition.Create(position.Latitude, position.Longitude, position.Altitude, position.Time ?? receivedAt, position.PrecisionBits);
        if (node.RecordPosition(fix, now))
        {
            await nodes.AddPositionAsync(NodePosition.Record(node.NodeNum, fix, receivedAt), cancellationToken);
        }
    }
}
