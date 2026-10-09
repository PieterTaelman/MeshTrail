using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Events;

/// <summary>Published when a message arrived from the mesh.</summary>
public sealed record MessageReceivedNotification(MessageDto Message) : INotification;

/// <summary>Published when one of our messages was queued or changed status (sent, acked, failed).</summary>
public sealed record MessageStatusChangedNotification(MessageDto Message) : INotification;

/// <summary>New messages appear in the open chat windows of the people who may see them (see <see cref="MessageAudience"/>).</summary>
public sealed class PushMessageReceivedToClientsHandler(IRealtimeNotifier notifier, MessageAudience audience)
    : INotificationHandler<MessageReceivedNotification>
{
    public const string EventName = "MessageReceived";

    public async ValueTask Handle(MessageReceivedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyUsersAsync(EventName, notification.Message, await audience.ForAsync(notification.Message, cancellationToken), cancellationToken);
}

/// <summary>Updates the status icon of a message (and the registration dialog) for the people who may see it.</summary>
public sealed class PushMessageStatusChangedToClientsHandler(IRealtimeNotifier notifier, MessageAudience audience)
    : INotificationHandler<MessageStatusChangedNotification>
{
    public const string EventName = "MessageStatusChanged";

    public async ValueTask Handle(MessageStatusChangedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyUsersAsync(EventName, notification.Message, await audience.ForAsync(notification.Message, cancellationToken), cancellationToken);
}
