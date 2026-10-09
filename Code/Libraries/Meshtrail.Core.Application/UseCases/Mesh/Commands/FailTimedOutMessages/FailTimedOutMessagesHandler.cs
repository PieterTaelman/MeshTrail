using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;

public sealed class FailTimedOutMessagesHandler(IMeshMessageRepository messages, IPublisher publisher)
    : ICommandHandler<FailTimedOutMessagesCommand, int>
{
    public const string TimeoutReason = "No delivery confirmation";
    public const string GatewayOfflineReason = "The gateway stayed offline";

    public async ValueTask<int> Handle(FailTimedOutMessagesCommand command, CancellationToken cancellationToken)
    {
        var timedOut = await messages.GetSentBeforeAsync(command.SentBefore, cancellationToken);
        var neverSent = await messages.GetQueuedBeforeAsync(command.QueuedBefore, cancellationToken);
        if (timedOut.Count == 0 && neverSent.Count == 0)
        {
            return 0;
        }

        foreach (var message in timedOut)
        {
            message.MarkFailed(TimeoutReason);
            await messages.UpdateAsync(message, cancellationToken);
        }

        foreach (var message in neverSent)
        {
            message.MarkFailed(GatewayOfflineReason);
            await messages.UpdateAsync(message, cancellationToken);
        }

        await messages.SaveChangesAsync(cancellationToken);
        foreach (var message in timedOut.Concat(neverSent))
        {
            await publisher.Publish(new MessageStatusChangedNotification(message.ToDto()), cancellationToken);
        }

        return timedOut.Count + neverSent.Count;
    }
}
