using Mediator;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Application.UseCases.Samples.Events;

/// <summary>Tells open browsers that samples changed, so their grid can refresh without polling.</summary>
public sealed class PushSampleChangedToClientsHandler(IRealtimeNotifier notifier) : INotificationHandler<SampleChangedNotification>
{
    public const string EventName = "samplesChanged";

    public async ValueTask Handle(SampleChangedNotification notification, CancellationToken cancellationToken) =>
        await notifier.NotifyAllAsync(
            EventName,
            new { id = notification.SampleId, kind = notification.Kind.ToString() },
            cancellationToken);
}
