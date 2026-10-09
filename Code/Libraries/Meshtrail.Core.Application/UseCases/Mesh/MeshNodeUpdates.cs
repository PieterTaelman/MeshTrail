using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>Repositories every "something arrived from the mesh about node X" handler needs.</summary>
public sealed record MeshNodeStores(
    IMeshNodeRepository Nodes,
    INodeReceptionRepository Receptions,
    IMeshGatewayRepository Gateways,
    INodeRegistrationRepository Registrations);

/// <summary>
/// The steps every "something arrived from the mesh about node X" handler shares:
/// load the node (or discover it), let the caller change it, save, tell the clients.
/// </summary>
internal static class MeshNodeUpdates
{
    public static async Task<MeshNode> ApplyAsync(
        MeshNodeStores stores,
        IPublisher publisher,
        uint nodeNum,
        DateTimeOffset now,
        Func<MeshNode, Task> change,
        CancellationToken cancellationToken)
    {
        var node = await stores.Nodes.GetAsync(nodeNum, cancellationToken);
        var isNew = node is null;
        node ??= MeshNode.Discover(nodeNum, now);

        await change(node);

        if (isNew)
        {
            await stores.Nodes.AddAsync(node, cancellationToken);
        }
        else
        {
            await stores.Nodes.UpdateAsync(node, cancellationToken);
        }

        // One save for the node and anything the change staged (position history, receptions).
        await stores.Nodes.SaveChangesAsync(cancellationToken);

        await PublishAsync(stores, publisher, node, now, cancellationToken);
        return node;
    }

    /// <summary>Tells the clients about the node's current state (also used when only its registration changed).</summary>
    public static async Task PublishAsync(MeshNodeStores stores, IPublisher publisher, MeshNode node, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var isGateway = (await stores.Gateways.GetActiveByNodeNumsAsync([node.NodeNum], cancellationToken)).Count > 0;
        var registered = await stores.Registrations.GetVerifiedNodeNumsAsync([node.NodeNum], cancellationToken);
        await publisher.Publish(new NodeUpdatedNotification(node.ToDto(now, isGateway, registered.Contains(node.NodeNum))), cancellationToken);
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
