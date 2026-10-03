using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Events;

/// <summary>Published when the gateway connection state (or its identity) changed.</summary>
public sealed record GatewayStatusChangedNotification(GatewayStatusDto Gateway) : INotification;

/// <summary>Updates the gateway chip in open browsers.</summary>
public sealed class PushGatewayStatusChangedToClientsHandler(IRealtimeNotifier notifier)
    : INotificationHandler<GatewayStatusChangedNotification>
{
    public const string EventName = "GatewayStatusChanged";

    public async ValueTask Handle(GatewayStatusChangedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyAllAsync(EventName, notification.Gateway, cancellationToken);
}
