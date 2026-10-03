using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Events;

/// <summary>Published when the answer to one of our traceroutes arrived.</summary>
public sealed record TracerouteCompletedNotification(NodeTracerouteDto Traceroute) : INotification;

/// <summary>Shows the route in the node detail panel as soon as it is known.</summary>
public sealed class PushTracerouteCompletedToClientsHandler(IRealtimeNotifier notifier)
    : INotificationHandler<TracerouteCompletedNotification>
{
    public const string EventName = "TracerouteCompleted";

    public async ValueTask Handle(TracerouteCompletedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyAllAsync(EventName, notification.Traceroute, cancellationToken);
}
