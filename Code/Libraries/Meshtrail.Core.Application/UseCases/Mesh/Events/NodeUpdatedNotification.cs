using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Events;

/// <summary>Published after a node was saved. Carries the full node so clients can update without reloading.</summary>
public sealed record NodeUpdatedNotification(NodeDto Node) : INotification;

/// <summary>Pushes changed nodes to open browsers (node list and map update live).</summary>
public sealed class PushNodeUpdatedToClientsHandler(IRealtimeNotifier notifier) : INotificationHandler<NodeUpdatedNotification>
{
    public const string EventName = "NodeUpdated";

    public async ValueTask Handle(NodeUpdatedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyAllAsync(EventName, notification.Node, cancellationToken);
}
