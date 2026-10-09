using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Events;

/// <summary>Published after a node was saved. Carries the full node so clients can update without reloading.</summary>
public sealed record NodeUpdatedNotification(NodeDto Node) : INotification;

/// <summary>
/// Pushes a changed node to the browsers watching its map area (node list and map update live). A node without a
/// position is on nobody's map, so it is not pushed.
/// </summary>
public sealed class PushNodeUpdatedToClientsHandler(IRealtimeNotifier notifier) : INotificationHandler<NodeUpdatedNotification>
{
    public const string EventName = "NodeUpdated";

    public async ValueTask Handle(NodeUpdatedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Node.Position is { } position)
        {
            await notifier.NotifyAreaAsync(EventName, notification.Node, position.Latitude, position.Longitude, cancellationToken);
        }
    }
}
