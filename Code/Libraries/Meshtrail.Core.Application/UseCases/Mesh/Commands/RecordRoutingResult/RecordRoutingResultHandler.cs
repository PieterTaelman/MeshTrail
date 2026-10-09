using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;

/// <summary>
/// Turns a delivery report into Acked or Failed. A direct message only counts as delivered when the destination
/// itself acknowledged; an "implicit" ack from a relay or the gateway only means it left, so it stays Sent.
/// </summary>
public sealed class RecordRoutingResultHandler(
    IMeshMessageRepository messages,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordRoutingResultCommand>
{
    public const string NoError = "None";

    public async ValueTask<Unit> Handle(RecordRoutingResultCommand command, CancellationToken cancellationToken)
    {
        var message = await messages.GetOutboundByPacketIdAsync(command.RequestId, cancellationToken);
        if (message is not { Status: MessageStatus.Queued or MessageStatus.Sent })
        {
            return Unit.Value;
        }

        var now = timeProvider.GetUtcNow();
        if (command.Error != NoError)
        {
            message.MarkFailed(command.Error);
        }
        else if (message.IsDirect && command.From != message.ToNodeNum)
        {
            return Unit.Value;
        }
        else
        {
            if (message.Status == MessageStatus.Queued)
            {
                // The report beat our own "sent" bookkeeping; it was obviously sent.
                message.MarkSent(now);
            }

            message.MarkAcked(now);
        }

        await messages.UpdateAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new MessageStatusChangedNotification(message.ToDto()), cancellationToken);
        return Unit.Value;
    }
}
