using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Events;

/// <summary>Published when a gateway was added, came online, went offline or was revoked.</summary>
public sealed record GatewayStatusChangedNotification(GatewayDto Gateway) : INotification;

/// <summary>Updates the gateway chip, the gateway layer and "My gateways" in open browsers.</summary>
public sealed class PushGatewayStatusChangedToClientsHandler(IRealtimeNotifier notifier)
    : INotificationHandler<GatewayStatusChangedNotification>
{
    public const string EventName = "GatewayStatusChanged";

    public async ValueTask Handle(GatewayStatusChangedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyAllAsync(EventName, notification.Gateway, cancellationToken);
}
